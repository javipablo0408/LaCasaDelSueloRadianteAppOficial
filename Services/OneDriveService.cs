using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Utilities;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class DeltaResponse
    {
        public List<OneDriveService.CambioRemoto> value { get; set; } = new();
    }

    public class OneDriveService
    {
        private readonly MauiMsalAuthService _auth;
        private readonly HttpClient _http;
        private readonly ILogger<OneDriveService> _logger;
        private readonly string localImagesPath = AppPaths.ImagesPath;
        private Timer _syncTimerImages;

        private const string RemoteFolder = "lacasadelsueloradianteapp";

        public enum ConflictStrategy
        {
            LocalPriority,
            CloudPriority,
            ManualMerge
        }

        public ConflictStrategy ConflictResolutionStrategy { get; set; } = ConflictStrategy.LocalPriority;

        public class CambioLocal
        {
            public string Tipo { get; set; }
            public string RutaLocal { get; set; }
            public string RutaRemota { get; set; }
        }

        public class CambioRemoto
        {
            public string Tipo { get; set; }
            public string Ruta { get; set; }
            public string Contenido { get; set; }
            public DateTime FechaModificacion { get; set; }
        }

        private string? _cachedAccessToken;
        private DateTimeOffset _tokenExpiration;
        private readonly AsyncRetryPolicy _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) => { });

        public OneDriveService(MauiMsalAuthService auth, HttpClient http, ILogger<OneDriveService> logger)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!Directory.Exists(AppPaths.BasePath))
                Directory.CreateDirectory(AppPaths.BasePath);

            if (!Directory.Exists(localImagesPath))
                Directory.CreateDirectory(localImagesPath);
        }

        private async Task AddAuthHeaderAsync()
        {
            if (_cachedAccessToken != null && DateTimeOffset.UtcNow < _tokenExpiration.AddMinutes(-5))
            {
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _cachedAccessToken);
                return;
            }

            var authResult = await _retryPolicy.ExecuteAsync(() => _auth.AcquireTokenAsync());
            _cachedAccessToken = authResult.AccessToken;
            _tokenExpiration = authResult.ExpiresOn;
            _logger.LogInformation("Token obtenido correctamente.");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _cachedAccessToken);
        }

        private string ComputeHashFromContent(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        public async Task UploadFileOptimizedAsync(string remotePath, string localFilePath)
        {
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota es inválida.", nameof(remotePath));
            if (!File.Exists(localFilePath))
                throw new FileNotFoundException("El archivo local no se encontró.", localFilePath);

            FileInfo fileInfo = new FileInfo(localFilePath);
            await AddAuthHeaderAsync();

            using var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            if (fileInfo.Length <= 4 * 1024 * 1024)
            {
                await UploadFileAsync(remotePath, stream);
            }
            else
            {
                await UploadLargeFileAsync(remotePath, stream);
            }
        }

        public async Task SincronizarImagenesAsync(CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();
            _logger.LogInformation("Inicio de sincronización de imágenes.");

            if (!Directory.Exists(localImagesPath))
            {
                _logger.LogInformation("Directorio '{0}' no existe. Creándolo.", localImagesPath);
                Directory.CreateDirectory(localImagesPath);
            }

            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
            var localFiles = Directory.GetFiles(localImagesPath)
                                      .Where(file => imageExtensions.Contains(Path.GetExtension(file)))
                                      .ToList();

            var tasks = localFiles.Select(localFile => Task.Run(async () =>
            {
                string fileName = Path.GetFileName(localFile);
                string remotePath = $"{RemoteFolder}/{fileName}";
                DateTime localModified = File.GetLastWriteTime(localFile);

                try
                {
                    string metadataUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}";
                    HttpResponseMessage metadataResponse = await _http.GetAsync(metadataUrl, ct);
                    DateTimeOffset remoteModified = DateTimeOffset.MinValue;
                    bool remoteExists = false;

                    if (metadataResponse.IsSuccessStatusCode)
                    {
                        string metadataJson = await metadataResponse.Content.ReadAsStringAsync(ct);
                        var remoteItem = JsonSerializer.Deserialize<OneDriveItem>(metadataJson,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (remoteItem?.lastModifiedDateTime != null)
                        {
                            remoteModified = remoteItem.lastModifiedDateTime.Value;
                            remoteExists = true;
                        }
                    }
                    else if (metadataResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        remoteExists = false;
                    }
                    else
                    {
                        _logger.LogWarning("Error al obtener metadatos para imagen '{0}': {1}", fileName, metadataResponse.ReasonPhrase);
                        return;
                    }

                    if (!remoteExists || localModified > remoteModified.LocalDateTime)
                    {
                        _logger.LogInformation("Subiendo imagen '{0}'.", fileName);
                        await UploadFileOptimizedAsync(remotePath, localFile);
                    }
                    else if (remoteExists && localModified < remoteModified.LocalDateTime)
                    {
                        _logger.LogInformation("Descargando imagen '{0}'.", fileName);
                        string downloadUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
                        HttpResponseMessage downloadResponse = await _http.GetAsync(downloadUrl, ct);
                        if (downloadResponse.IsSuccessStatusCode)
                        {
                            using var stream = await downloadResponse.Content.ReadAsStreamAsync(ct);
                            using var fileStream = new FileStream(localFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                            await stream.CopyToAsync(fileStream, ct);
                        }
                        else
                        {
                            _logger.LogWarning("Error al descargar imagen '{0}': {1}", fileName, downloadResponse.ReasonPhrase);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Sin cambios para imagen '{0}'.", fileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sincronizando imagen '{0}'", fileName);
                }
            }));

            await Task.WhenAll(tasks);
            _logger.LogInformation("Sincronización de imágenes finalizada.");
        }

        // --- Sincronización a nivel de registros (JSON) ---
        public async Task DescargarYFusionarCambiosDeTodosLosDispositivosAsync(DatabaseService db, CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();
            string folderUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{RemoteFolder}:/children";
            var response = await _http.GetAsync(folderUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudo listar archivos de sincronización remota.");
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
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
                var fileResponse = await _http.GetAsync(requestUrl, ct);
                if (!fileResponse.IsSuccessStatusCode)
                    continue;

                var fileJson = await fileResponse.Content.ReadAsStringAsync(ct);
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
                return; // Nada que subir

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

        public async Task DescargarYFusionarCambiosRemotosAsync(DatabaseService db, CancellationToken ct = default)
        {
            // NOTA: Para producción, deberías listar todos los archivos sync_*.json de todos los dispositivos.
            // Aquí solo se descarga el archivo del dispositivo actual para simplificar.
            var remotePath = $"{RemoteFolder}/sync_{Environment.MachineName}.json";
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
            await AddAuthHeaderAsync();
            var response = await _http.GetAsync(requestUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("No se encontraron cambios remotos para este dispositivo.");
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var payload = JsonSerializer.Deserialize<SyncPayload>(json);

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
            _logger.LogInformation("Cambios remotos descargados y fusionados.");
        }

        public async Task SincronizarRegistrosAsync(DatabaseService db, CancellationToken ct = default)
        {
            await SubirCambiosLocalesAsync(db, ct);
            await DescargarYFusionarCambiosDeTodosLosDispositivosAsync(db, ct);
        }

        // --- Métodos originales (no modificados) ---

        public async Task SincronizarBidireccionalAsync(CancellationToken ct, DatabaseService databaseService)
        {
            await AddAuthHeaderAsync();
            _logger.LogInformation("Inicia sincronización bidireccional.");

            try
            {
                var cambiosLocales = DetectarCambiosLocales();
                _logger.LogInformation("Se detectaron {0} cambios locales.", cambiosLocales.Count);
                var cambiosRemotos = await ObtenerCambiosRemotosAsync(ct);
                _logger.LogInformation("Se detectaron {0} cambios remotos.", cambiosRemotos.Count);

                // Procesar cambios locales
                foreach (var cambio in cambiosLocales)
                {
                    string nombreArchivo = Path.GetFileName(cambio.RutaLocal);
                    var remoteCambio = cambiosRemotos
                        .FirstOrDefault(cr => !string.IsNullOrEmpty(cr.Ruta) &&
                                              cr.Ruta.Equals(nombreArchivo, StringComparison.OrdinalIgnoreCase));

                    if (remoteCambio != null)
                    {
                        string localHash = HashUtility.ComputeSHA256(cambio.RutaLocal);
                        string remoteHash = ComputeHashFromContent(remoteCambio.Contenido);
                        _logger.LogInformation("Comparando '{0}' local({1}) vs remoto({2}).", nombreArchivo, localHash, remoteHash);
                        if (string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase))
                            continue;

                        DateTime fechaLocal = File.GetLastWriteTime(cambio.RutaLocal);
                        if (fechaLocal > remoteCambio.FechaModificacion)
                        {
                            _logger.LogInformation("El archivo '{0}' es más reciente localmente.", nombreArchivo);
                            if (ConflictResolutionStrategy == ConflictStrategy.LocalPriority || (!ExisteConflicto(remoteCambio)))
                            {
                                if (cambio.Tipo == "Archivo")
                                {
                                    using var stream = new FileStream(cambio.RutaLocal, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                                    await UploadFileAsync(cambio.RutaRemota, stream);
                                    _logger.LogInformation("Archivo '{0}' subido.", nombreArchivo);
                                }
                                else if (cambio.Tipo == "BaseDeDatos")
                                {
                                    await BackupBaseDeDatosAsync(cambio.RutaLocal);
                                }
                            }
                            else if (ConflictResolutionStrategy == ConflictStrategy.ManualMerge)
                            {
                                await ResolverConflictoAsync(remoteCambio, cambio.RutaLocal);
                                using var stream = new FileStream(cambio.RutaLocal, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                                await UploadFileAsync(cambio.RutaRemota, stream);
                            }
                        }
                        else if (fechaLocal < remoteCambio.FechaModificacion)
                        {
                            _logger.LogInformation("El archivo '{0}' es más reciente en la nube.", nombreArchivo);
                            if (ConflictResolutionStrategy == ConflictStrategy.CloudPriority || (!ExisteConflicto(remoteCambio)))
                            {
                                AplicarCambioRemoto(remoteCambio, databaseService);
                                _logger.LogInformation("Archivo '{0}' actualizado localmente.", nombreArchivo);
                            }
                            else if (ConflictResolutionStrategy == ConflictStrategy.ManualMerge)
                            {
                                await ResolverConflictoAsync(remoteCambio, cambio.RutaLocal);
                                AplicarCambioRemoto(remoteCambio, databaseService);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No existe versión remota para '{0}', subiendo nuevo.", nombreArchivo);
                        if (cambio.Tipo == "Archivo")
                        {
                            using var stream = new FileStream(cambio.RutaLocal, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                            await UploadFileAsync(cambio.RutaRemota, stream);
                        }
                        else if (cambio.Tipo == "BaseDeDatos")
                        {
                            await BackupBaseDeDatosAsync(cambio.RutaLocal);
                        }
                    }
                }

                // Procesar registros remotos que no existen localmente
                foreach (var remoteCambio in cambiosRemotos)
                {
                    if (string.IsNullOrEmpty(remoteCambio.Ruta))
                        continue;
                    string rutaLocal = Path.Combine(AppPaths.BasePath, remoteCambio.Ruta);
                    if (!File.Exists(rutaLocal))
                    {
                        AplicarCambioRemoto(remoteCambio, databaseService);
                        _logger.LogInformation("Archivo '{0}' descargado desde la nube.", remoteCambio.Ruta);
                    }
                }

                // Revisar archivos locales eliminados en OneDrive para re-sincronizarlos
                var remoteFileNames = cambiosRemotos
                    .Where(cr => !string.IsNullOrEmpty(cr.Ruta))
                    .Select(cr => cr.Ruta)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var localFiles = Directory.GetFiles(AppPaths.BasePath);
                foreach (var localFile in localFiles)
                {
                    var fileName = Path.GetFileName(localFile);
                    if (!remoteFileNames.Contains(fileName))
                    {
                        string remotePath = $"{RemoteFolder}/{fileName}";
                        using var stream = new FileStream(localFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                        await UploadFileAsync(remotePath, stream);
                        _logger.LogInformation("Archivo re-subido por falta en la nube: {0}", fileName);
                    }
                }

                RegistrarSincronizacion();
                _logger.LogInformation("Sincronización bidireccional finalizada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la sincronización bidireccional");
                throw;
            }
        }

        private void AplicarCambioRemoto(CambioRemoto cambioRemoto, DatabaseService databaseService)
        {
            if (string.IsNullOrEmpty(cambioRemoto.Ruta))
                return;
            if (cambioRemoto.Tipo == "Archivo")
            {
                string rutaLocal = Path.Combine(AppPaths.BasePath, cambioRemoto.Ruta);
                Directory.CreateDirectory(Path.GetDirectoryName(rutaLocal)!);
                File.WriteAllText(rutaLocal, cambioRemoto.Contenido);
                _logger.LogInformation("Se aplicó el cambio remoto para archivo: {0}", rutaLocal);
            }
            else if (cambioRemoto.Tipo == "BaseDeDatos")
            {
                databaseService.RestaurarBaseDeDatosDesdeContenido(cambioRemoto.Contenido);
                _logger.LogInformation("Base de datos restaurada desde contenido remoto.");
            }
        }

        public async Task RestaurarBaseDeDatosAsync(string localPath)
        {
            await AddAuthHeaderAsync();
            string remotePath = $"{RemoteFolder}/clientes.db3";
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";

            HttpResponseMessage response = await _http.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error al descargar la base de datos: {response.ReasonPhrase}");

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await stream.CopyToAsync(fileStream);
        }

        public async Task DescargarImagenSiNoExisteAsync(string localPath, string remotePath)
        {
            if (!File.Exists(localPath))
            {
                await AddAuthHeaderAsync();
                string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
                var response = await _http.GetAsync(requestUrl);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Imagen no encontrada en OneDrive: {0}", remotePath);
                        return;
                    }
                    else
                    {
                        throw new Exception($"Error al descargar la imagen: {response.ReasonPhrase}");
                    }
                }
                string directorio = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                    Directory.CreateDirectory(directorio);

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await stream.CopyToAsync(fileStream);
            }
        }

        public List<CambioLocal> DetectarCambiosLocales()
        {
            var cambios = new List<CambioLocal>();
            if (!Directory.Exists(AppPaths.BasePath))
                Directory.CreateDirectory(AppPaths.BasePath);

            DateTime lastSync = ObtenerUltimaSincronizacion();
            _logger.LogInformation("Última sincronización: {0}", lastSync);

            var archivosLocales = Directory.GetFiles(AppPaths.BasePath);
            foreach (var archivo in archivosLocales)
            {
                var infoArchivo = new FileInfo(archivo);
                if (infoArchivo.LastWriteTime > lastSync)
                {
                    cambios.Add(new CambioLocal
                    {
                        Tipo = "Archivo",
                        RutaLocal = archivo,
                        RutaRemota = $"{RemoteFolder}/{infoArchivo.Name}"
                    });
                }
            }

            string rutaBD = AppPaths.DatabasePath;
            if (File.Exists(rutaBD) && File.GetLastWriteTime(rutaBD) > lastSync)
            {
                cambios.Add(new CambioLocal
                {
                    Tipo = "BaseDeDatos",
                    RutaLocal = rutaBD,
                    RutaRemota = $"{RemoteFolder}/clientes.db3"
                });
            }
            return cambios;
        }

        private async Task<List<CambioRemoto>> ObtenerCambiosRemotosAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _http.GetAsync("https://graph.microsoft.com/v1.0/me/drive/root/delta", ct);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(ct);
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var delta = JsonSerializer.Deserialize<DeltaResponse>(json, options);
                var cambios = delta?.value ?? new List<CambioRemoto>();
                foreach (var item in cambios)
                {
                    if (item.Tipo == null && item.Ruta == null)
                        item.Tipo = "Eliminado";
                }
                return cambios;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error al deserializar delta");
                return new List<CambioRemoto>();
            }
        }

        private bool ExisteConflicto(CambioRemoto cambioRemoto)
        {
            string rutaLocal = Path.Combine(AppPaths.BasePath, cambioRemoto.Ruta);
            if (File.Exists(rutaLocal))
            {
                DateTime fechaLocal = File.GetLastWriteTime(rutaLocal);
                return fechaLocal > cambioRemoto.FechaModificacion;
            }
            return false;
        }

        // NOTA: Este método ya no debe usarse directamente para la base de datos, solo para archivos.
        private void AplicarCambioRemoto(CambioRemoto cambioRemoto)
        {
            // Método legacy, solo para compatibilidad. No usar para base de datos.
            if (string.IsNullOrEmpty(cambioRemoto.Ruta))
                return;
            if (cambioRemoto.Tipo == "Archivo")
            {
                string rutaLocal = Path.Combine(AppPaths.BasePath, cambioRemoto.Ruta);
                Directory.CreateDirectory(Path.GetDirectoryName(rutaLocal)!);
                File.WriteAllText(rutaLocal, cambioRemoto.Contenido);
                _logger.LogInformation("Se aplicó el cambio remoto para archivo: {0}", rutaLocal);
            }
        }

        private class SyncState
        {
            public DateTime UltimaSincronizacion { get; set; }
        }

        private void RegistrarSincronizacion()
        {
            var syncState = new SyncState { UltimaSincronizacion = DateTime.UtcNow };
            string rutaConfig = Path.Combine(AppPaths.BasePath, "sync_state.json");
            File.WriteAllTextAsync(rutaConfig, JsonSerializer.Serialize(syncState, new JsonSerializerOptions { WriteIndented = true }))
                .GetAwaiter().GetResult();
            _logger.LogInformation("Se registró la sincronización.");
        }

        private DateTime ObtenerUltimaSincronizacion()
        {
            string rutaConfig = Path.Combine(AppPaths.BasePath, "sync_state.json");
            if (!File.Exists(rutaConfig))
                return DateTime.MinValue;
            try
            {
                string json = File.ReadAllTextAsync(rutaConfig).GetAwaiter().GetResult();
                var state = JsonSerializer.Deserialize<SyncState>(json);
                return state?.UltimaSincronizacion ?? DateTime.MinValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo sync state");
                return DateTime.MinValue;
            }
        }

        public async Task SincronizarTodoAsync(CancellationToken ct = default, DatabaseService databaseService = null)
        {
            if (databaseService != null)
                await SincronizarBidireccionalAsync(ct, databaseService);
            else
                await SincronizarBidireccionalAsync(ct, null);
            await SincronizarImagenesAsync(ct);
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
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            _logger.LogInformation("Temporizador de sincronización de imágenes iniciado.");
        }

        public async Task BackupBaseDeDatosAsync(string backupPath = null)
        {
            string rutaBackup = backupPath ?? AppPaths.DatabasePath;
            if (!File.Exists(rutaBackup))
                throw new FileNotFoundException("No se encontró la base de datos para backup.", rutaBackup);

            string rutaRemota = $"{RemoteFolder}/clientes.db3";
            await UploadFileOptimizedAsync(rutaRemota, rutaBackup);
            _logger.LogInformation("Backup de base de datos realizado desde {0}.", rutaBackup);
        }

        public async Task UploadFileAsync(string remotePath, Stream content)
        {
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota es inválida.", nameof(remotePath));
            await AddAuthHeaderAsync();
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
            using var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            HttpResponseMessage response = await _http.PutAsync(requestUrl, streamContent);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error al subir archivo: {response.ReasonPhrase}");
            _logger.LogInformation("Archivo subido a {0}.", remotePath);
        }

        public async Task UploadLargeFileAsync(string remotePath, Stream content)
        {
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota es inválida.", nameof(remotePath));
            await AddAuthHeaderAsync();
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/createUploadSession";
            var sessionResponse = await _http.PostAsync(requestUrl, null);
            if (!sessionResponse.IsSuccessStatusCode)
                throw new Exception($"Error al crear sesión de carga: {sessionResponse.ReasonPhrase}");
            string sessionContent = await sessionResponse.Content.ReadAsStringAsync();
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
                while ((bytesRead = await content.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    long byteRangeStart = totalBytesRead;
                    long byteRangeEnd = totalBytesRead + bytesRead - 1;
                    using var fragmentContent = new ByteArrayContent(buffer, 0, bytesRead);
                    fragmentContent.Headers.ContentRange = new ContentRangeHeaderValue(byteRangeStart, byteRangeEnd, content.Length);
                    fragmentContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    var fragmentResponse = await _http.PutAsync(session.UploadUrl, fragmentContent);
                    if (!fragmentResponse.IsSuccessStatusCode && fragmentResponse.StatusCode != System.Net.HttpStatusCode.Accepted)
                        throw new Exception($"Error al subir fragmento: {fragmentResponse.ReasonPhrase}");
                    totalBytesRead += bytesRead;
                }
            }
        }

        public async Task<string> CreateShareLinkAsync(string remotePath, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota es inválida.", nameof(remotePath));
            await AddAuthHeaderAsync();
            string requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/createLink";
            var body = new { type = "view" };
            string jsonBody = JsonSerializer.Serialize(body);
            using var contentJson = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _http.PostAsync(requestUrl, contentJson, ct);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error al crear enlace compartido: {response.ReasonPhrase}");
            string responseContent = await response.Content.ReadAsStringAsync(ct);
            var permission = JsonSerializer.Deserialize<PermissionResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            if (permission?.Link == null || string.IsNullOrEmpty(permission.Link.WebUrl))
                throw new Exception("No se obtuvo el enlace compartido.");
            return permission.Link.WebUrl;
        }

        private async Task ResolverConflictoAsync(CambioRemoto remoteCambio, string rutaLocal)
        {
            string fileName = Path.GetFileName(rutaLocal);
            string conflictFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_conflict_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(fileName)}";
            string remoteConflictPath = $"{RemoteFolder}/{conflictFileName}";
            using var stream = new FileStream(rutaLocal, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await UploadFileAsync(remoteConflictPath, stream);
            _logger.LogInformation("Conflicto resuelto: copia remota guardada como {0}", conflictFileName);
        }

        private class UploadSession
        {
            public string? UploadUrl { get; set; }
            public DateTimeOffset ExpirationDateTime { get; set; }
        }

        private class PermissionResponse
        {
            public ShareLink? Link { get; set; }
        }

        private class ShareLink
        {
            public string? WebUrl { get; set; }
        }

        public class OneDriveItem
        {
            public string? name { get; set; }
            public DateTimeOffset? lastModifiedDateTime { get; set; }
        }

        public class OneDriveItemsResponse
        {
            public List<OneDriveItem> value { get; set; } = new();
        }
    }
}