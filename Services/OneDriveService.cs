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
        private readonly string localImagesPath = AppPaths.BasePath;
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

            var authResult = await _retryPolicy.ExecuteAsync(() => _auth.AcquireTokenAsync()).ConfigureAwait(false);
            _cachedAccessToken = authResult.AccessToken;
            _tokenExpiration = authResult.ExpiresOn;
            _logger.LogInformation("Token obtenido correctamente.");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _cachedAccessToken);
        }

        // --- Sincronización de imágenes ---
        public async Task SincronizarImagenesAsync(CancellationToken ct = default)
        {
            if (!HayConexion())
            {
                _logger.LogWarning("Sincronización de imágenes pospuesta: sin conexión.");
                return;
            }

            await AddAuthHeaderAsync().ConfigureAwait(false);
            _logger.LogInformation("Inicio de sincronización de imágenes (comparación local vs OneDrive).");

            if (!Directory.Exists(localImagesPath))
                Directory.CreateDirectory(localImagesPath);

            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

            // 1. Lista de imágenes locales
            var localFiles = Directory.GetFiles(localImagesPath)
                .Where(file => imageExtensions.Contains(Path.GetExtension(file)))
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 2. Lista de imágenes en OneDrive
            string folderUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{RemoteFolder}:/children";
            var response = await _http.GetAsync(folderUrl, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudo listar archivos de imágenes remotas.");
                return;
            }
            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var items = JsonSerializer.Deserialize<OneDriveItemsResponse>(json);
            var remoteFiles = items?.value
                .Where(i => i.name != null && imageExtensions.Contains(Path.GetExtension(i.name)))
                .Select(i => i.name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

            // 3. Descargar imágenes que están en OneDrive pero no localmente
            foreach (var remoteFile in remoteFiles.Except(localFiles))
            {
                var localPath = Path.Combine(localImagesPath, remoteFile);
                _logger.LogInformation("[SYNC] Descargando imagen faltante localmente: {0}", remoteFile);
                await DescargarImagenSiNoExisteAsync(localPath, $"{RemoteFolder}/{remoteFile}");
            }

            // 4. Subir imágenes que están localmente pero no en OneDrive
            foreach (var localFile in localFiles.Except(remoteFiles))
            {
                var localPath = Path.Combine(localImagesPath, localFile);
                _logger.LogInformation("[SYNC] Subiendo imagen faltante en OneDrive: {0}", localFile);
                await UploadFileOptimizedAsync($"{RemoteFolder}/{localFile}", localPath);
            }

            _logger.LogInformation("Sincronización de imágenes finalizada.");
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
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
            using var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            HttpResponseMessage response = await _http.PutAsync(requestUrl, streamContent).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error al subir archivo: {response.ReasonPhrase}");
            _logger.LogInformation("Archivo subido a {0}.", remotePath);
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
        }

        public async Task SubirCambiosLocalesAsync(DatabaseService db, CancellationToken ct = default)
        {
            var clientes = await db.ObtenerClientesNoSincronizadosAsync(ct);
            var servicios = await db.ObtenerServiciosNoSincronizadosAsync(ct);

            if (!clientes.Any() && !servicios.Any())
                return;

            var payload = new SyncPayload
            {
                Clientes = clientes,
                Servicios = servicios
            };

            var json = JsonSerializer.Serialize(payload);
            var remotePath = $"{RemoteFolder}/sync_{Environment.MachineName}.json";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            await UploadFileAsync(remotePath, ms);

            await db.MarcarClientesComoSincronizadosAsync(clientes.Select(c => c.Id), ct);
            await db.MarcarServiciosComoSincronizadosAsync(servicios.Select(s => s.Id), ct);
            _logger.LogInformation("Cambios locales subidos y marcados como sincronizados.");
        }

        public async Task DescargarYFusionarCambiosDeTodosLosDispositivosAsync(DatabaseService db, CancellationToken ct = default)
        {
            await AddAuthHeaderAsync().ConfigureAwait(false);
            string folderUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{RemoteFolder}:/children";
            var response = await _http.GetAsync(folderUrl, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudo listar archivos de sincronización remota.");
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var items = JsonSerializer.Deserialize<OneDriveItemsResponse>(json);

            var syncFiles = items?.value
                .Where(i => i.name != null && i.name.StartsWith("sync_") && i.name.EndsWith(".json"))
                .ToList();

            if (syncFiles == null || syncFiles.Count == 0)
            {
                _logger.LogInformation("No se encontraron archivos de sincronización remota.");
                return;
            }

            foreach (var file in syncFiles)
            {
                string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{RemoteFolder}/{file.name}:/content";
                var fileResponse = await _http.GetAsync(requestUrl, ct).ConfigureAwait(false);
                if (!fileResponse.IsSuccessStatusCode)
                    continue;

                var fileJson = await fileResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var payload = JsonSerializer.Deserialize<SyncPayload>(fileJson);

                if (payload?.Clientes != null)
                {
                    foreach (var cliente in payload.Clientes)
                        await db.InsertarOActualizarClienteAsync(cliente, ct);
                }
                if (payload?.Servicios != null)
                {
                    foreach (var servicio in payload.Servicios)
                        await db.InsertarOActualizarServicioAsync(servicio, ct);
                }
            }
            _logger.LogInformation("Cambios remotos de todos los dispositivos descargados y fusionados.");
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
                    await SincronizarImagenesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en la sincronización automática de imágenes");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(2));

            _logger.LogInformation("Temporizador de sincronización de imágenes iniciado (cada 2 minutos).");
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
}