using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Utilities;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Microsoft.Maui.Networking;
using LaCasaDelSueloRadianteApp.Models;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class OneDriveService
    {
        private readonly MauiMsalAuthService _auth;
        private readonly HttpClient _http;
        private readonly ILogger<OneDriveService> _logger;
        private readonly string localImagesPath = AppPaths.ImagesPath; // Usar ImagesPath en lugar de BasePath
        private Timer _syncTimerImages;

        private const string RemoteFolder = "lacasadelsueloradianteapp";

        public OneDriveService(MauiMsalAuthService auth, HttpClient http, ILogger<OneDriveService> logger)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!Directory.Exists(AppPaths.BasePath))
                Directory.CreateDirectory(AppPaths.BasePath);
        }

        private bool HayConexion()
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }

        private string? _cachedAccessToken;
        private DateTimeOffset _tokenExpiration;
        private readonly AsyncRetryPolicy _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) => { });

        private async Task AddAuthHeaderAsync()
        {
            if (!HayConexion())
                throw new InvalidOperationException("No hay conexión a Internet para autenticación OneDrive.");

            if (_cachedAccessToken != null && DateTimeOffset.UtcNow < _tokenExpiration.AddMinutes(-5))
            {
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _cachedAccessToken);
                return;
            }

            var authResult = await _retryPolicy.ExecuteAsync(() => _auth.AcquireTokenSilentAsync()).ConfigureAwait(false);
            if (authResult == null)
                throw new InvalidOperationException("No se pudo obtener el token de acceso. El usuario no está autenticado.");

            _cachedAccessToken = authResult.AccessToken;
            _tokenExpiration = authResult.ExpiresOn;
            _logger.LogInformation("Token obtenido correctamente.");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _cachedAccessToken);
        }

        // --- Sincronización de imágenes mejorada ---
        public async Task SincronizarImagenesAsync(CancellationToken ct = default)
        {
            if (!HayConexion())
            {
                _logger.LogWarning("Sincronización de imágenes pospuesta: sin conexión.");
                return;
            }

            try
            {
                await AddAuthHeaderAsync().ConfigureAwait(false);
                _logger.LogInformation("Inicio de sincronización bilateral de imágenes.");

                if (!Directory.Exists(localImagesPath))
                {
                    Directory.CreateDirectory(localImagesPath);
                    _logger.LogInformation($"Directorio de imágenes creado: {localImagesPath}");
                }

                var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

                // 1. Obtener lista de archivos locales con metadatos
                _logger.LogInformation($"Escaneando archivos locales en: {localImagesPath}");
                var localFiles = Directory.GetFiles(localImagesPath)
                    .Where(file => imageExtensions.Contains(Path.GetExtension(file)))
                    .Select(filePath => new LocalFileInfo
                    {
                        Name = Path.GetFileName(filePath),
                        FullPath = filePath,
                        LastModified = File.GetLastWriteTimeUtc(filePath),
                        Size = new FileInfo(filePath).Length
                    })
                    .ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation($"Archivos locales encontrados: {localFiles.Count}");
                foreach (var file in localFiles.Values)
                {
                    _logger.LogDebug($"Local: {file.Name} - {file.LastModified:yyyy-MM-dd HH:mm:ss} - {file.Size} bytes");
                }

                // 2. Obtener lista de archivos remotos con metadatos
                _logger.LogInformation("Consultando archivos remotos en OneDrive...");
                var remoteFiles = await ObtenerListaImagenesRemotas(ct);
                
                _logger.LogInformation($"Archivos remotos encontrados: {remoteFiles.Count}");
                foreach (var file in remoteFiles.Values)
                {
                    _logger.LogDebug($"Remoto: {file.Name} - {file.LastModified:yyyy-MM-dd HH:mm:ss} - {file.Size} bytes");
                }

                // 3. Determinar acciones de sincronización
                _logger.LogInformation("Determinando acciones de sincronización...");
                var accionesSinc = DeterminarAccionesSincronizacion(localFiles, remoteFiles);
                
                _logger.LogInformation($"Acciones determinadas: {accionesSinc.Count}");
                foreach (var accion in accionesSinc)
                {
                    _logger.LogInformation($"Acción: {accion.Accion} - {accion.Archivo} - {accion.Motivo}");
                }

                // 4. Ejecutar acciones de sincronización
                if (accionesSinc.Any())
                {
                    _logger.LogInformation("Ejecutando acciones de sincronización...");
                    await EjecutarAccionesSincronizacion(accionesSinc, ct);
                }
                else
                {
                    _logger.LogInformation("No hay acciones de sincronización pendientes.");
                }

                _logger.LogInformation($"Sincronización de imágenes completada. Descargadas: {accionesSinc.Count(a => a.Accion == AccionSincronizacion.Descargar)}, Subidas: {accionesSinc.Count(a => a.Accion == AccionSincronizacion.Subir)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la sincronización de imágenes");
                throw;
            }
        }

        private async Task<Dictionary<string, RemoteFileInfo>> ObtenerListaImagenesRemotas(CancellationToken ct)
        {
            var remoteFiles = new Dictionary<string, RemoteFileInfo>(StringComparer.OrdinalIgnoreCase);
            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

            try
            {
                string folderUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{RemoteFolder}:/children";
                _logger.LogInformation($"Consultando OneDrive: {folderUrl}");
                
                var response = await _http.GetAsync(folderUrl, ct).ConfigureAwait(false);
                _logger.LogInformation($"Respuesta de OneDrive: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    _logger.LogDebug($"JSON respuesta: {json.Substring(0, Math.Min(200, json.Length))}...");
                    
                    var items = JsonSerializer.Deserialize<OneDriveItemsResponse>(json);

                    if (items?.value != null)
                    {
                        _logger.LogInformation($"Items encontrados en OneDrive: {items.value.Count}");
                        
                        foreach (var item in items.value)
                        {
                            if (item.name != null)
                            {
                                _logger.LogDebug($"Examinando archivo: {item.name}");
                                
                                if (imageExtensions.Contains(Path.GetExtension(item.name)))
                                {
                                    var fileInfo = new RemoteFileInfo
                                    {
                                        Name = item.name,
                                        LastModified = (item.lastModifiedDateTime ?? DateTimeOffset.MinValue).UtcDateTime,
                                        Size = item.size ?? 0
                                    };
                                    
                                    remoteFiles[item.name] = fileInfo;
                                    _logger.LogDebug($"Imagen remota agregada: {item.name} - {fileInfo.LastModified:yyyy-MM-dd HH:mm:ss}");
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("La respuesta de OneDrive no contiene items válidos");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    _logger.LogWarning($"No se pudo obtener lista de archivos remotos: {response.StatusCode} - {response.ReasonPhrase}. Error: {errorContent}");
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Error de deserialización JSON al obtener lista de imágenes remotas");
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Error de red al obtener lista de imágenes remotas");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al obtener lista de imágenes remotas");
            }

            _logger.LogInformation($"Total de imágenes remotas procesadas: {remoteFiles.Count}");
            return remoteFiles;
        }

        private List<AccionSincronizacionImagen> DeterminarAccionesSincronizacion(
            Dictionary<string, LocalFileInfo> localFiles,
            Dictionary<string, RemoteFileInfo> remoteFiles)
        {
            var acciones = new List<AccionSincronizacionImagen>();

            // Procesar archivos que existen en ambos lados
            foreach (var localFile in localFiles.Values)
            {
                if (remoteFiles.TryGetValue(localFile.Name, out var remoteFile))
                {
                    // Archivo existe en ambos lados - determinar cuál es más reciente
                    if (localFile.LastModified > remoteFile.LastModified.AddSeconds(1)) // Margen de 1 segundo para diferencias de precisión
                    {
                        acciones.Add(new AccionSincronizacionImagen
                        {
                            Archivo = localFile.Name,
                            Accion = AccionSincronizacion.Subir,
                            RutaLocal = localFile.FullPath,
                            Motivo = $"Archivo local más reciente ({localFile.LastModified:yyyy-MM-dd HH:mm:ss} vs {remoteFile.LastModified:yyyy-MM-dd HH:mm:ss})"
                        });
                    }
                    else if (remoteFile.LastModified > localFile.LastModified.AddSeconds(1))
                    {
                        acciones.Add(new AccionSincronizacionImagen
                        {
                            Archivo = localFile.Name,
                            Accion = AccionSincronizacion.Descargar,
                            RutaLocal = localFile.FullPath,
                            Motivo = $"Archivo remoto más reciente ({remoteFile.LastModified:yyyy-MM-dd HH:mm:ss} vs {localFile.LastModified:yyyy-MM-dd HH:mm:ss})"
                        });
                    }
                    // Si las fechas son iguales (dentro del margen), no hacer nada
                }
                else
                {
                    // Archivo solo existe localmente - subir
                    acciones.Add(new AccionSincronizacionImagen
                    {
                        Archivo = localFile.Name,
                        Accion = AccionSincronizacion.Subir,
                        RutaLocal = localFile.FullPath,
                        Motivo = "Archivo solo existe localmente"
                    });
                }
            }

            // Procesar archivos que solo existen remotamente
            foreach (var remoteFile in remoteFiles.Values)
            {
                if (!localFiles.ContainsKey(remoteFile.Name))
                {
                    acciones.Add(new AccionSincronizacionImagen
                    {
                        Archivo = remoteFile.Name,
                        Accion = AccionSincronizacion.Descargar,
                        RutaLocal = Path.Combine(localImagesPath, remoteFile.Name),
                        Motivo = "Archivo solo existe remotamente"
                    });
                }
            }

            return acciones;
        }

        private async Task EjecutarAccionesSincronizacion(List<AccionSincronizacionImagen> acciones, CancellationToken ct)
        {
            foreach (var accion in acciones)
            {
                try
                {
                    _logger.LogInformation($"[SYNC IMG] {accion.Accion}: {accion.Archivo} - {accion.Motivo}");

                    switch (accion.Accion)
                    {
                        case AccionSincronizacion.Subir:
                            await UploadFileOptimizedAsync($"{RemoteFolder}/{accion.Archivo}", accion.RutaLocal);
                            break;

                        case AccionSincronizacion.Descargar:
                            await DescargarImagenParaSincronizacionAsync(accion.RutaLocal, $"{RemoteFolder}/{accion.Archivo}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error al ejecutar acción {accion.Accion} para {accion.Archivo}");
                }
            }
        }

        /// <summary>
        /// Descarga una imagen para sincronización, sobrescribiendo si ya existe (bilateral sync)
        /// </summary>
        private async Task DescargarImagenParaSincronizacionAsync(string localFullPath, string remotePath)
        {
            _logger.LogInformation("[SYNC IMG DOWNLOAD] Descargando: {0} desde {1}", localFullPath, remotePath);

            if (string.IsNullOrEmpty(localFullPath) || string.IsNullOrEmpty(remotePath))
            {
                _logger.LogWarning("[SYNC IMG DOWNLOAD] Ruta local o remota vacía.");
                return;
            }

            await AddAuthHeaderAsync().ConfigureAwait(false);
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
            _logger.LogInformation("[SYNC IMG DOWNLOAD] URL de descarga: {0}", requestUrl);

            var response = await _http.GetAsync(requestUrl).ConfigureAwait(false);
            _logger.LogInformation("[SYNC IMG DOWNLOAD] Código de estado HTTP: {0}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[SYNC IMG DOWNLOAD] Imagen no encontrada en OneDrive: {0} - Código: {1} - Mensaje: {2}", remotePath, response.StatusCode, response.ReasonPhrase);
                return;
            }

            string? directorio = Path.GetDirectoryName(localFullPath);
            if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                Directory.CreateDirectory(directorio);

            // Sobrescribir el archivo si ya existe (para sincronización bilateral)
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var fileStream = new FileStream(localFullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);
            _logger.LogInformation("[SYNC IMG DOWNLOAD] Imagen descargada y guardada en: {0}", localFullPath);
        }

        // --- Descarga de archivos genérica ---
        public async Task<Stream?> DescargarArchivoAsync(string remoteFileName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(remoteFileName))
                throw new ArgumentException("El nombre del archivo remoto no puede ser nulo o vacío.", nameof(remoteFileName));

            await AddAuthHeaderAsync().ConfigureAwait(false);

            string remotePath = $"{RemoteFolder}/{remoteFileName}";
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";

            _logger.LogInformation("[DescargarArchivoAsync] Intentando descargar archivo remoto: {0} (requestUrl: {1})", remoteFileName, requestUrl);

            var response = await _http.GetAsync(requestUrl, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[DescargarArchivoAsync] No se pudo descargar el archivo '{0}' desde OneDrive. Código: {1} - Mensaje: {2}", remoteFileName, response.StatusCode, response.ReasonPhrase);
                return null;
            }

            _logger.LogInformation("[DescargarArchivoAsync] Archivo '{0}' descargado correctamente.", remoteFileName);
            return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        }

        public async Task DescargarImagenSiNoExisteAsync(string localFullPath, string remotePath)
        {
            _logger.LogInformation("[DescargarImagenSiNoExisteAsync] Intentando descargar: {0} desde {1}", localFullPath, remotePath);

            if (string.IsNullOrEmpty(localFullPath) || string.IsNullOrEmpty(remotePath))
            {
                _logger.LogWarning("[DescargarImagenSiNoExisteAsync] Ruta local o remota vacía.");
                return;
            }

            if (File.Exists(localFullPath))
            {
                _logger.LogWarning("[DescargarImagenSiNoExisteAsync] El archivo ya existe localmente: {0}", localFullPath);
                return;
            }

            await AddAuthHeaderAsync().ConfigureAwait(false);
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
            _logger.LogInformation("[DescargarImagenSiNoExisteAsync] URL de descarga: {0}", requestUrl);

            var response = await _http.GetAsync(requestUrl).ConfigureAwait(false);
            _logger.LogInformation("[DescargarImagenSiNoExisteAsync] Código de estado HTTP: {0}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[DescargarImagenSiNoExisteAsync] Imagen no encontrada en OneDrive: {0} - Código: {1} - Mensaje: {2}", remotePath, response.StatusCode, response.ReasonPhrase);
                return;
            }

            string? directorio = Path.GetDirectoryName(localFullPath);
            if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                Directory.CreateDirectory(directorio);

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var fileStream = new FileStream(localFullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);
            _logger.LogInformation("[DescargarImagenSiNoExisteAsync] Imagen descargada y guardada en: {0}", localFullPath);
        }

        public async Task UploadFileOptimizedAsync(string remotePath, string localFilePath)
        {
            _logger.LogInformation("[UploadFileOptimizedAsync] Subiendo archivo: localFilePath={0}, remotePath={1}", localFilePath, remotePath);

            if (!HayConexion())
            {
                _logger.LogWarning("No hay conexión. Se pospone la subida de {0}", localFilePath);
                return;
            }

            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota es inválida.", nameof(remotePath));
            if (!File.Exists(localFilePath))
                throw new FileNotFoundException("El archivo local no se encontró.", localFilePath);

            FileInfo fileInfo = new FileInfo(localFilePath);
            await AddAuthHeaderAsync().ConfigureAwait(false);

            using var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            if (fileInfo.Length <= 4 * 1024 * 1024)
            {
                await UploadFileAsync(remotePath, stream).ConfigureAwait(false);
            }
            else
            {
                await UploadLargeFileAsync(remotePath, stream).ConfigureAwait(false);
            }
        }

        public async Task UploadFileAsync(string remotePath, Stream content)
        {
            _logger.LogInformation("[UploadFileAsync] Subiendo archivo a: {0}", remotePath);

            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota es inválida.", nameof(remotePath));
            
            await AddAuthHeaderAsync().ConfigureAwait(false);
            
            // Asegurar que la carpeta existe antes de subir el archivo
            await EnsureFolderExistsAsync(remotePath).ConfigureAwait(false);
            
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
            using var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            HttpResponseMessage response = await _http.PutAsync(requestUrl, streamContent).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error al subir archivo: {response.ReasonPhrase}");
            _logger.LogInformation("Archivo subido a {0}.", remotePath);
        }

        /// <summary>
        /// Asegura que la carpeta especificada en la ruta existe, creándola si es necesario
        /// </summary>
        private async Task EnsureFolderExistsAsync(string remotePath)
        {
            if (string.IsNullOrEmpty(remotePath))
                return;

            // Extraer la ruta de la carpeta (todo excepto el nombre del archivo)
            var pathParts = remotePath.Split('/');
            if (pathParts.Length <= 1)
                return; // No hay carpetas que crear

            var folderPath = string.Join("/", pathParts.Take(pathParts.Length - 1));
            
            try
            {
                // Verificar si la carpeta ya existe
                string checkUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{folderPath}";
                var checkResponse = await _http.GetAsync(checkUrl).ConfigureAwait(false);
                
                if (checkResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[EnsureFolderExists] Carpeta ya existe: {0}", folderPath);
                    return; // La carpeta ya existe
                }
                
                // Crear la carpeta si no existe
                await CreateFolderAsync(folderPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EnsureFolderExists] Error al verificar/crear carpeta: {0}", folderPath);
                // Continuar con la subida del archivo - podría funcionar si la carpeta ya existe
            }
        }

        /// <summary>
        /// Crea una carpeta en OneDrive (incluyendo carpetas padre si no existen)
        /// </summary>
        private async Task CreateFolderAsync(string folderPath)
        {
            var pathParts = folderPath.Split('/');
            var currentPath = "";

            // Crear carpetas una por una, desde la raíz hacia abajo
            foreach (var folderName in pathParts)
            {
                if (string.IsNullOrEmpty(folderName))
                    continue;

                var parentPath = string.IsNullOrEmpty(currentPath) ? "root" : $"root:/{currentPath}";
                currentPath = string.IsNullOrEmpty(currentPath) ? folderName : $"{currentPath}/{folderName}";

                try
                {
                    // Verificar si esta carpeta específica ya existe
                    string checkUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{currentPath}";
                    var checkResponse = await _http.GetAsync(checkUrl).ConfigureAwait(false);
                    
                    if (checkResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("[CreateFolder] Carpeta ya existe: {0}", currentPath);
                        continue; // Esta carpeta ya existe, continuar con la siguiente
                    }

                    // Crear la carpeta
                    string createUrl = $"https://graph.microsoft.com/v1.0/me/drive/{parentPath}/children";
                    var folderData = new Dictionary<string, object>
                    {
                        { "name", folderName },
                        { "folder", new { } },
                        { "@microsoft.graph.conflictBehavior", "rename" }
                    };

                    var jsonContent = JsonSerializer.Serialize(folderData);
                    using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    
                    var createResponse = await _http.PostAsync(createUrl, content).ConfigureAwait(false);
                    
                    if (createResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("[CreateFolder] Carpeta creada exitosamente: {0}", currentPath);
                    }
                    else
                    {
                        var errorContent = await createResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        _logger.LogWarning("[CreateFolder] No se pudo crear la carpeta {0}: {1} - {2}", 
                            currentPath, createResponse.StatusCode, errorContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CreateFolder] Error al crear carpeta: {0}", currentPath);
                }
            }
        }

        public async Task UploadLargeFileAsync(string remotePath, Stream content)
        {
            _logger.LogInformation("[UploadLargeFileAsync] Subiendo archivo grande a: {0}", remotePath);

            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota es inválida.", nameof(remotePath));
            await AddAuthHeaderAsync().ConfigureAwait(false);
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/createUploadSession";
            var sessionResponse = await _http.PostAsync(requestUrl, null).ConfigureAwait(false);
            if (!sessionResponse.IsSuccessStatusCode)
                throw new Exception($"Error al crear sesión de carga: {sessionResponse.ReasonPhrase}");
            string sessionContent = await sessionResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var session = JsonSerializer.Deserialize<UploadSession>(sessionContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            if (session == null || string.IsNullOrEmpty(session.UploadUrl))
                throw new Exception("No se pudo obtener la URL de la sesión de carga.");
            const int fragmentSize = 320 * 1024;
            var buffer = new byte[fragmentSize];
            long totalBytesRead = 0;
            int bytesRead;
            using (content)
            {
                while ((bytesRead = await content.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    long byteRangeStart = totalBytesRead;
                    long byteRangeEnd = totalBytesRead + bytesRead - 1;
                    using var fragmentContent = new ByteArrayContent(buffer, 0, bytesRead);
                    fragmentContent.Headers.ContentRange = new ContentRangeHeaderValue(byteRangeStart, byteRangeEnd, content.Length);
                    fragmentContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    var fragmentResponse = await _http.PutAsync(session.UploadUrl, fragmentContent).ConfigureAwait(false);
                    if (!fragmentResponse.IsSuccessStatusCode && fragmentResponse.StatusCode != System.Net.HttpStatusCode.Accepted)
                        throw new Exception($"Error al subir fragmento: {fragmentResponse.ReasonPhrase}");
                    totalBytesRead += bytesRead;
                }
            }
        }

        // --- Sincronización de registros (clientes y servicios) ---

        public class SyncPayload
        {
            public List<Cliente> Clientes { get; set; } = new();
            public List<Servicio> Servicios { get; set; } = new();
            public List<Instalador> Instaladores { get; set; } = new();
        }

        public async Task SubirCambiosLocalesAsync(DatabaseService db, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("[SUBIR CAMBIOS] Iniciando subida de cambios locales...");
                
                var clientes = await db.ObtenerClientesNoSincronizadosAsync(ct);
                var servicios = await db.ObtenerServiciosNoSincronizadosAsync(ct);
                var instaladores = await db.ObtenerInstaladoresNoSincronizadosAsync(ct);

                _logger.LogInformation("[SUBIR CAMBIOS] Encontrados: {ClientesCount} clientes, {ServiciosCount} servicios, {InstaladoresCount} instaladores no sincronizados",
                    clientes.Count, servicios.Count, instaladores.Count);

                if (!clientes.Any() && !servicios.Any() && !instaladores.Any())
                {
                    _logger.LogInformation("[SUBIR CAMBIOS] No hay cambios locales pendientes.");
                    return;
                }

                // Log de servicios específicamente para debug
                if (servicios.Any())
                {
                    _logger.LogInformation("[SUBIR CAMBIOS] Servicios a sincronizar:");
                    foreach (var servicio in servicios)
                    {
                        _logger.LogInformation("  - Servicio ID:{ServicioId}, Cliente:{ClienteId}, Fecha:{Fecha}", 
                            servicio.Id, servicio.ClienteId, servicio.Fecha);
                    }
                }

                var payload = new SyncPayload
                {
                    Clientes = clientes,
                    Servicios = servicios,
                    Instaladores = instaladores
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                var remotePath = $"{RemoteFolder}/sync_{Environment.MachineName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                
                _logger.LogInformation("[SUBIR CAMBIOS] Subiendo a: {RemotePath}", remotePath);
                
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
                await UploadFileAsync(remotePath, ms);

                await db.MarcarClientesComoSincronizadosAsync(clientes.Select(c => c.Id), ct);
                await db.MarcarServiciosComoSincronizadosAsync(servicios.Select(s => s.Id), ct);
                await db.MarcarInstaladoresComoSincronizadosAsync(instaladores.Select(i => i.Id), ct);
                
                _logger.LogInformation("[SUBIR CAMBIOS] Cambios locales subidos y marcados como sincronizados: {ClientesCount} clientes, {ServiciosCount} servicios, {InstaladoresCount} instaladores",
                    clientes.Count, servicios.Count, instaladores.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SUBIR CAMBIOS] Error al subir cambios locales");
                throw;
            }
        }

        public async Task DescargarYFusionarCambiosDeTodosLosDispositivosAsync(DatabaseService db, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("[DESCARGAR CAMBIOS] Iniciando descarga de cambios remotos...");
                
                await AddAuthHeaderAsync().ConfigureAwait(false);
                string folderUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{RemoteFolder}:/children";
                var response = await _http.GetAsync(folderUrl, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[DESCARGAR CAMBIOS] No se pudo listar archivos de sincronización remota: {StatusCode}", response.StatusCode);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var items = JsonSerializer.Deserialize<OneDriveItemsResponse>(json);

                var syncFiles = items?.value
                    .Where(i => i.name != null && i.name.StartsWith("sync_") && i.name.EndsWith(".json"))
                    .ToList();

                if (syncFiles == null || syncFiles.Count == 0)
                {
                    _logger.LogInformation("[DESCARGAR CAMBIOS] No se encontraron archivos de sincronización remota.");
                    return;
                }

                _logger.LogInformation("[DESCARGAR CAMBIOS] Encontrados {Count} archivos de sincronización", syncFiles.Count);

                int totalClientesFusionados = 0;
                int totalServiciosFusionados = 0;
                int totalInstaladoresFusionados = 0;

                foreach (var file in syncFiles)
                {
                    try
                    {
                        _logger.LogInformation("[DESCARGAR CAMBIOS] Procesando archivo: {FileName}", file.name);
                        
                        string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{RemoteFolder}/{file.name}:/content";
                        var fileResponse = await _http.GetAsync(requestUrl, ct).ConfigureAwait(false);
                        if (!fileResponse.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("[DESCARGAR CAMBIOS] No se pudo descargar archivo: {FileName} - {StatusCode}", file.name, fileResponse.StatusCode);
                            continue;
                        }

                        var fileJson = await fileResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        var payload = JsonSerializer.Deserialize<SyncPayload>(fileJson);

                        if (payload?.Clientes != null && payload.Clientes.Any())
                        {
                            _logger.LogInformation("[DESCARGAR CAMBIOS] Fusionando {Count} clientes de {FileName}", payload.Clientes.Count, file.name);
                            foreach (var cliente in payload.Clientes)
                                await db.InsertarOActualizarClienteAsync(cliente, ct);
                            totalClientesFusionados += payload.Clientes.Count;
                        }
                        
                        if (payload?.Servicios != null && payload.Servicios.Any())
                        {
                            _logger.LogInformation("[DESCARGAR CAMBIOS] Fusionando {Count} servicios de {FileName}", payload.Servicios.Count, file.name);
                            foreach (var servicio in payload.Servicios)
                            {
                                _logger.LogInformation("  - Fusionando Servicio ID:{ServicioId}, Cliente:{ClienteId}, Fecha:{Fecha}", 
                                    servicio.Id, servicio.ClienteId, servicio.Fecha);
                                await db.InsertarOActualizarServicioAsync(servicio, ct);
                            }
                            totalServiciosFusionados += payload.Servicios.Count;
                        }
                        
                        if (payload?.Instaladores != null && payload.Instaladores.Any())
                        {
                            _logger.LogInformation("[DESCARGAR CAMBIOS] Fusionando {Count} instaladores de {FileName}", payload.Instaladores.Count, file.name);
                            foreach (var instalador in payload.Instaladores)
                                await db.InsertarOActualizarInstaladorAsync(instalador, ct);
                            totalInstaladoresFusionados += payload.Instaladores.Count;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[DESCARGAR CAMBIOS] Error procesando archivo: {FileName}", file.name);
                    }
                }
                
                _logger.LogInformation("[DESCARGAR CAMBIOS] Fusión completada: {ClientesCount} clientes, {ServiciosCount} servicios, {InstaladoresCount} instaladores",
                    totalClientesFusionados, totalServiciosFusionados, totalInstaladoresFusionados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DESCARGAR CAMBIOS] Error general al descargar y fusionar cambios");
                throw;
            }
        }

        public async Task SincronizarRegistrosAsync(DatabaseService db, CancellationToken ct = default)
        {
            await SubirCambiosLocalesAsync(db, ct);
            await DescargarYFusionarCambiosDeTodosLosDispositivosAsync(db, ct);
        }

        // --- Limpieza de archivos de sincronización antiguos ---
        public async Task LimpiarArchivosSyncAntiguosAsync(int dias = 30, CancellationToken ct = default)
        {
            await AddAuthHeaderAsync().ConfigureAwait(false);
            string folderUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{RemoteFolder}:/children";
            var response = await _http.GetAsync(folderUrl, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var items = JsonSerializer.Deserialize<OneDriveItemsResponse>(json);

            var fechaLimite = DateTimeOffset.UtcNow.AddDays(-dias);

            var archivosAntiguos = items?.value
                .Where(i => i.name != null && i.name.StartsWith("sync_") && i.name.EndsWith(".json") && i.lastModifiedDateTime < fechaLimite)
                .ToList();

            if (archivosAntiguos != null)
            {
                foreach (var file in archivosAntiguos)
                {
                    string deleteUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{RemoteFolder}/{file.name}";
                    await _http.DeleteAsync(deleteUrl, ct).ConfigureAwait(false);
                    _logger.LogInformation("Archivo de sincronización antiguo eliminado: {0}", file.name);
                }
            }
        }

        // --- Restaurar base de datos desde OneDrive ---
        public async Task RestaurarBaseDeDatosAsync(string localPath)
        {
            await AddAuthHeaderAsync().ConfigureAwait(false);
            string remotePath = $"{RemoteFolder}/clientes.db3";
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";

            HttpResponseMessage response = await _http.GetAsync(requestUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error al descargar la base de datos: {response.ReasonPhrase}");

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);
        }

        // --- Sincronización total y automática ---
        public async Task SincronizarTodoAsync(CancellationToken ct = default, DatabaseService databaseService = null)
        {
            if (databaseService != null)
                await SincronizarRegistrosAsync(databaseService, ct).ConfigureAwait(false);
            await SincronizarImagenesAsync(ct).ConfigureAwait(false);
            await LimpiarArchivosSyncAntiguosAsync(30, ct).ConfigureAwait(false);
            _logger.LogInformation("Sincronización total finalizada.");
        }

        public void IniciarSincronizacionAutomaticaImagenes()
        {
            _syncTimerImages = new Timer(async state =>
            {
                try
                {
                    if (HayConexion())
                    {
                        await SincronizarImagenesAsync();
                    }
                    else
                    {
                        _logger.LogInformation("Sincronización automática de imágenes omitida: sin conexión a Internet");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en la sincronización automática de imágenes");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(3)); // Cada 3 minutos para imágenes específicamente

            _logger.LogInformation("Temporizador de sincronización de imágenes iniciado (cada 3 minutos).");
        }

        /// <summary>
        /// Detiene la sincronización automática de imágenes
        /// </summary>
        public void DetenerSincronizacionAutomaticaImagenes()
        {
            _syncTimerImages?.Change(Timeout.Infinite, 0);
            _syncTimerImages?.Dispose();
            _syncTimerImages = null;
            _logger.LogInformation("Sincronización automática de imágenes detenida.");
        }

        /// <summary>
        /// Método de diagnóstico para verificar el estado de sincronización de imágenes
        /// </summary>
        public async Task<SyncStatistics> DiagnosticarSincronizacionImagenesAsync(CancellationToken ct = default)
        {
            var stats = new SyncStatistics
            {
                HayConexion = HayConexion()
            };

            try
            {
                _logger.LogInformation("[DIAGNÓSTICO] Iniciando diagnóstico de sincronización de imágenes...");

                // Verificar directorio local
                _logger.LogInformation($"[DIAGNÓSTICO] Directorio local: {localImagesPath}");
                _logger.LogInformation($"[DIAGNÓSTICO] Directorio existe: {Directory.Exists(localImagesPath)}");

                // También verificar directorio base por si hay imágenes mal ubicadas
                var baseDirectory = AppPaths.BasePath;
                _logger.LogInformation($"[DIAGNÓSTICO] Directorio base: {baseDirectory}");

                if (Directory.Exists(localImagesPath))
                {
                    var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
                    var localFiles = Directory.GetFiles(localImagesPath)
                        .Where(file => imageExtensions.Contains(Path.GetExtension(file)))
                        .ToList();

                    stats.ImagenesLocales = localFiles.Count;
                    stats.TamañoImagenesLocalesMB = localFiles.Sum(f => new FileInfo(f).Length) / (1024.0 * 1024.0);

                    _logger.LogInformation($"[DIAGNÓSTICO] Imágenes locales: {stats.ImagenesLocales}");
                    _logger.LogInformation($"[DIAGNÓSTICO] Tamaño total local: {stats.TamañoImagenesLocalesMB:F2} MB");

                    foreach (var file in localFiles.Take(5)) // Solo mostrar las primeras 5
                    {
                        var info = new FileInfo(file);
                        _logger.LogInformation($"[DIAGNÓSTICO] Local: {Path.GetFileName(file)} - {info.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss} - {info.Length} bytes");
                    }

                    // Verificar si hay imágenes mal ubicadas en el directorio base
                    var imagenesEnUbicacionIncorrecta = Directory.GetFiles(baseDirectory)
                        .Where(file => imageExtensions.Contains(Path.GetExtension(file)))
                        .Where(file => !file.StartsWith(localImagesPath))
                        .ToList();

                    if (imagenesEnUbicacionIncorrecta.Any())
                    {
                        _logger.LogWarning($"[DIAGNÓSTICO] Imágenes en ubicación INCORRECTA: {imagenesEnUbicacionIncorrecta.Count}");
                        foreach (var file in imagenesEnUbicacionIncorrecta.Take(3))
                        {
                            _logger.LogWarning($"[DIAGNÓSTICO] Mal ubicada: {Path.GetFileName(file)}");
                        }
                    }
                }

                // Verificar archivos remotos si hay conexión
                if (stats.HayConexion)
                {
                    _logger.LogInformation("[DIAGNÓSTICO] Verificando archivos remotos...");
                    
                    try
                    {
                        await AddAuthHeaderAsync().ConfigureAwait(false);
                        var remoteFiles = await ObtenerListaImagenesRemotas(ct);

                        stats.ImagenesRemotas = remoteFiles.Count;
                        stats.TamañoImagenesRemotasMB = remoteFiles.Values.Sum(f => f.Size) / (1024.0 * 1024.0);

                        _logger.LogInformation($"[DIAGNÓSTICO] Imágenes remotas: {stats.ImagenesRemotas}");
                        _logger.LogInformation($"[DIAGNÓSTICO] Tamaño total remoto: {stats.TamañoImagenesRemotasMB:F2} MB");

                        foreach (var file in remoteFiles.Values.Take(5)) // Solo mostrar las primeras 5
                        {
                            _logger.LogInformation($"[DIAGNÓSTICO] Remoto: {file.Name} - {file.LastModified:yyyy-MM-dd HH:mm:ss} - {file.Size} bytes");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[DIAGNÓSTICO] Error al consultar archivos remotos");
                    }
                }
                else
                {
                    _logger.LogWarning("[DIAGNÓSTICO] Sin conexión - no se pueden verificar archivos remotos");
                }

                stats.UltimaSincronizacion = Preferences.ContainsKey("LastSyncTimestamp")
                    ? DateTime.FromBinary(Preferences.Get("LastSyncTimestamp", 0))
                    : (DateTime?)null;

                _logger.LogInformation($"[DIAGNÓSTICO] Última sincronización: {stats.UltimaSincronizacion?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Nunca"}");
                _logger.LogInformation("[DIAGNÓSTICO] Diagnóstico completado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAGNÓSTICO] Error durante el diagnóstico");
            }

            return stats;
        }

        /// <summary>
        /// Fuerza una sincronización inmediata de imágenes para diagnóstico
        /// </summary>
        public async Task ForzarSincronizacionImagenesAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("[FORZAR SYNC] Iniciando sincronización forzada de imágenes...");
            try
            {
                await SincronizarImagenesAsync(ct);
                _logger.LogInformation("[FORZAR SYNC] Sincronización forzada completada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FORZAR SYNC] Error en sincronización forzada");
                throw;
            }
        }

        public async Task<List<SyncQueue>> DescargarSyncQueueDesdeOneDriveAsync(CancellationToken ct = default)
        {
            using var stream = await DescargarArchivoAsync("syncqueue.json", ct);
            if (stream == null)
                return new List<SyncQueue>();
            return await JsonSerializer.DeserializeAsync<List<SyncQueue>>(stream, cancellationToken: ct) ?? new();
        }

        // --- Modelos auxiliares para deserialización ---
        public class OneDriveItem
        {
            public string? name { get; set; }
            public DateTimeOffset? lastModifiedDateTime { get; set; }
            public long? size { get; set; }
        }

        public class OneDriveItemsResponse
        {
            public List<OneDriveItem> value { get; set; } = new();
        }

        private class UploadSession
        {
            public string? UploadUrl { get; set; }
            public DateTimeOffset ExpirationDateTime { get; set; }
        }
    }

    // --- Clases de soporte para sincronización de imágenes ---
    public class LocalFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
    }

    public class RemoteFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
    }

    public class AccionSincronizacionImagen
    {
        public string Archivo { get; set; } = string.Empty;
        public AccionSincronizacion Accion { get; set; }
        public string RutaLocal { get; set; } = string.Empty;
        public string Motivo { get; set; } = string.Empty;
    }

    public enum AccionSincronizacion
    {
        Subir,
        Descargar,
        NoAction
    }

    /// <summary>
    /// Estadísticas de sincronización para monitoreo
    /// </summary>
    public class SyncStatistics
    {
        public int ImagenesLocales { get; set; }
        public int ImagenesRemotas { get; set; }
        public double TamañoImagenesLocalesMB { get; set; }
        public double TamañoImagenesRemotasMB { get; set; }
        public DateTime? UltimaSincronizacion { get; set; }
        public bool HayConexion { get; set; }
        public string EstadoSincronizacion => HayConexion ? "Conectado" : "Sin conexión";
    }
}