using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Utilities;

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

        // Ruta local de imágenes (ajústala si es necesario)
        private readonly string localImagesPath = @"C:\Users\Javier\AppData\Local\Packages\com.companyname.lacasadelsueloradianteapp_9zz4h110yvjzm\LocalState";

        // Campo para el temporizador (para evitar que sea recolectado por el GC)
        private Timer? _syncTimerImages;

        public static string RutaBaseLocal { get; } =
            Path.Combine(FileSystem.AppDataDirectory, "ArchivosLocal");

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

        public OneDriveService(MauiMsalAuthService auth)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _http = new HttpClient();

            if (!Directory.Exists(RutaBaseLocal))
                Directory.CreateDirectory(RutaBaseLocal);
        }

        private async Task AddAuthHeaderAsync()
        {
            var authResult = await _auth.AcquireTokenAsync();
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        }

        /// <summary>
        /// Calcula el hash SHA256 a partir del contenido (texto) recibido.
        /// </summary>
        private string ComputeHashFromContent(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Realiza la sincronización bidireccional (archivos y base de datos).
        /// </summary>
        public async Task SincronizarBidireccionalAsync(CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();

            try
            {
                var cambiosLocales = DetectarCambiosLocales();
                var cambiosRemotos = await ObtenerCambiosRemotosAsync(ct);

                // Procesa cambios locales
                foreach (var cambio in cambiosLocales)
                {
                    var nombreArchivo = Path.GetFileName(cambio.RutaLocal);
                    var remoteCambio = cambiosRemotos.FirstOrDefault(cr => !string.IsNullOrEmpty(cr.Ruta) &&
                                                     cr.Ruta.Equals(nombreArchivo, StringComparison.OrdinalIgnoreCase));

                    // Si ya existe versión remota, se compara fecha y hash
                    if (remoteCambio != null)
                    {
                        string localHash = HashUtility.ComputeSHA256(cambio.RutaLocal);
                        string remoteHash = ComputeHashFromContent(remoteCambio.Contenido);

                        if (string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var fechaLocal = File.GetLastWriteTime(cambio.RutaLocal);

                        if (fechaLocal > remoteCambio.FechaModificacion)
                        {
                            if (ConflictResolutionStrategy == ConflictStrategy.LocalPriority || (!ExisteConflicto(remoteCambio)))
                            {
                                if (cambio.Tipo == "Archivo")
                                {
                                    using var stream = File.OpenRead(cambio.RutaLocal);
                                    await UploadFileAsync(cambio.RutaRemota, stream);
                                }
                                else if (cambio.Tipo == "BaseDeDatos")
                                {
                                    await BackupBaseDeDatosAsync();
                                }
                            }
                            else if (ConflictResolutionStrategy == ConflictStrategy.ManualMerge)
                            {
                                await ResolverConflictoAsync(remoteCambio, cambio.RutaLocal);
                                using var stream = File.OpenRead(cambio.RutaLocal);
                                await UploadFileAsync(cambio.RutaRemota, stream);
                            }
                        }
                        else if (fechaLocal < remoteCambio.FechaModificacion)
                        {
                            if (ConflictResolutionStrategy == ConflictStrategy.CloudPriority || (!ExisteConflicto(remoteCambio)))
                            {
                                AplicarCambioRemoto(remoteCambio);
                            }
                            else if (ConflictResolutionStrategy == ConflictStrategy.ManualMerge)
                            {
                                await ResolverConflictoAsync(remoteCambio, cambio.RutaLocal);
                                AplicarCambioRemoto(remoteCambio);
                            }
                        }
                    }
                    else
                    {
                        // Si no existe versión remota, se sube el registro (archivo o BD)
                        if (cambio.Tipo == "Archivo")
                        {
                            using var stream = File.OpenRead(cambio.RutaLocal);
                            await UploadFileAsync(cambio.RutaRemota, stream);
                        }
                        else if (cambio.Tipo == "BaseDeDatos")
                        {
                            await BackupBaseDeDatosAsync();
                        }
                    }
                }

                // Procesa registros remotos que no existen localmente
                foreach (var cambioRemoto in cambiosRemotos)
                {
                    if (string.IsNullOrEmpty(cambioRemoto.Ruta))
                        continue;

                    var rutaLocal = Path.Combine(RutaBaseLocal, cambioRemoto.Ruta);
                    if (!File.Exists(rutaLocal))
                    {
                        AplicarCambioRemoto(cambioRemoto);
                    }
                }

                // Revisar archivos locales eliminados en OneDrive para re-sincronizarlos
                var remoteFileNames = cambiosRemotos.Where(cr => !string.IsNullOrEmpty(cr.Ruta)).Select(cr => cr.Ruta)
                                                      .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var localFiles = Directory.GetFiles(RutaBaseLocal);
                foreach (var localFile in localFiles)
                {
                    var fileName = Path.GetFileName(localFile);
                    if (!remoteFileNames.Contains(fileName))
                    {
                        var remotePath = $"lacasadelsueloradianteapp/{fileName}";
                        using var stream = File.OpenRead(localFile);
                        await UploadFileAsync(remotePath, stream);
                        Debug.WriteLine($"Archivo re-subido al detectar eliminación remota: {fileName}");
                    }
                }

                RegistrarSincronizacion();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en la sincronización: {ex.Message}");
                throw;
            }
        }

        public async Task RestaurarBaseDeDatosAsync(string localPath)
        {
            await AddAuthHeaderAsync();

            var remotePath = "lacasadelsueloradianteapp/clientes.db3";
            var requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";

            var response = await _http.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error al descargar la base de datos: {response.ReasonPhrase}");

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(localPath);
            await stream.CopyToAsync(fileStream);
        }

        public async Task DescargarImagenSiNoExisteAsync(string localPath, string remotePath)
        {
            if (!File.Exists(localPath))
            {
                await AddAuthHeaderAsync();

                var requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
                var response = await _http.GetAsync(requestUrl);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Debug.WriteLine($"Archivo no encontrado en OneDrive: {remotePath}");
                        return;
                    }
                    else
                    {
                        throw new Exception($"Error al descargar la imagen: {response.ReasonPhrase}");
                    }
                }

                var directorio = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                    Directory.CreateDirectory(directorio);

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(localPath);
                await stream.CopyToAsync(fileStream);
            }
        }

        public List<CambioLocal> DetectarCambiosLocales()
        {
            var cambios = new List<CambioLocal>();

            if (!Directory.Exists(RutaBaseLocal))
                Directory.CreateDirectory(RutaBaseLocal);

            var archivosLocales = Directory.GetFiles(RutaBaseLocal);
            foreach (var archivo in archivosLocales)
            {
                var infoArchivo = new FileInfo(archivo);
                if (infoArchivo.LastWriteTime > ObtenerUltimaSincronizacion(archivo))
                {
                    cambios.Add(new CambioLocal
                    {
                        Tipo = "Archivo",
                        RutaLocal = archivo,
                        RutaRemota = $"lacasadelsueloradianteapp/{infoArchivo.Name}"
                    });
                }
            }

            var rutaBaseDeDatos = Path.Combine(FileSystem.AppDataDirectory, "clientes.db3");
            if (File.Exists(rutaBaseDeDatos) && File.GetLastWriteTime(rutaBaseDeDatos) > ObtenerUltimaSincronizacion(rutaBaseDeDatos))
            {
                cambios.Add(new CambioLocal
                {
                    Tipo = "BaseDeDatos",
                    RutaLocal = rutaBaseDeDatos,
                    RutaRemota = "lacasadelsueloradianteapp/clientes.db3"
                });
            }

            return cambios;
        }

        private async Task<List<CambioRemoto>> ObtenerCambiosRemotosAsync(CancellationToken ct)
        {
            var response = await _http.GetAsync("https://graph.microsoft.com/v1.0/me/drive/root/delta", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
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
                Debug.WriteLine($"Error al deserializar el JSON: {ex.Message}");
                return new List<CambioRemoto>();
            }
        }

        private bool ExisteConflicto(CambioRemoto cambioRemoto)
        {
            var rutaLocal = Path.Combine(RutaBaseLocal, cambioRemoto.Ruta);
            if (File.Exists(rutaLocal))
            {
                var fechaLocal = File.GetLastWriteTime(rutaLocal);
                return fechaLocal > cambioRemoto.FechaModificacion;
            }
            return false;
        }

        private void AplicarCambioRemoto(CambioRemoto cambioRemoto)
        {
            if (string.IsNullOrEmpty(cambioRemoto.Ruta))
                return;

            if (cambioRemoto.Tipo == "Archivo")
            {
                var rutaLocal = Path.Combine(RutaBaseLocal, cambioRemoto.Ruta);
                Directory.CreateDirectory(Path.GetDirectoryName(rutaLocal)!);
                File.WriteAllText(rutaLocal, cambioRemoto.Contenido);
            }
            else if (cambioRemoto.Tipo == "BaseDeDatos")
            {
                var rutaBaseDeDatos = Path.Combine(FileSystem.AppDataDirectory, "clientes.db3");
                File.WriteAllText(rutaBaseDeDatos, cambioRemoto.Contenido);
            }
        }

        private void RegistrarSincronizacion()
        {
            var ultimaSincronizacion = DateTime.UtcNow;
            var rutaArchivoConfig = Path.Combine(FileSystem.AppDataDirectory, "sync_state.json");
            var estadoSincronizacion = new { UltimaSincronizacion = ultimaSincronizacion };
            var json = JsonSerializer.Serialize(estadoSincronizacion, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(rutaArchivoConfig, json);
        }

        private DateTime ObtenerUltimaSincronizacion(string archivo)
        {
            var rutaArchivoConfig = Path.Combine(FileSystem.AppDataDirectory, "sync_state.json");
            if (!File.Exists(rutaArchivoConfig))
                return DateTime.MinValue;

            var json = File.ReadAllText(rutaArchivoConfig);
            var estadoSincronizacion = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
            if (estadoSincronizacion != null && estadoSincronizacion.TryGetValue(archivo, out var ultimaSincronizacion))
                return ultimaSincronizacion;
            return DateTime.MinValue;
        }

        public async Task BackupBaseDeDatosAsync()
        {
            var rutaBaseDeDatos = Path.Combine(FileSystem.AppDataDirectory, "clientes.db3");

            if (!File.Exists(rutaBaseDeDatos))
                throw new FileNotFoundException("No se encontró la base de datos para realizar el backup.", rutaBaseDeDatos);

            using var stream = new FileStream(rutaBaseDeDatos, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var rutaRemota = "lacasadelsueloradianteapp/clientes.db3";
            await UploadFileAsync(rutaRemota, stream);
        }

        public async Task UploadFileAsync(string remotePath, Stream content)
        {
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota no puede ser nula o vacía.", nameof(remotePath));

            await AddAuthHeaderAsync();

            var requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
            using var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var response = await _http.PutAsync(requestUrl, streamContent);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error al subir el archivo: {response.ReasonPhrase}");
        }

        public async Task UploadLargeFileAsync(string remotePath, Stream content)
        {
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota no puede ser nula o vacía.", nameof(remotePath));

            await AddAuthHeaderAsync();

            var requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/createUploadSession";
            var sessionResponse = await _http.PostAsync(requestUrl, null);
            if (!sessionResponse.IsSuccessStatusCode)
                throw new Exception($"Error al crear la sesión de carga: {sessionResponse.ReasonPhrase}");

            var sessionContent = await sessionResponse.Content.ReadAsStringAsync();
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
                    var byteRangeStart = totalBytesRead;
                    var byteRangeEnd = totalBytesRead + bytesRead - 1;

                    using var fragmentContent = new ByteArrayContent(buffer, 0, bytesRead);
                    fragmentContent.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(byteRangeStart, byteRangeEnd, content.Length);
                    fragmentContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                    var fragmentResponse = await _http.PutAsync(session.UploadUrl, fragmentContent);
                    if (!fragmentResponse.IsSuccessStatusCode && fragmentResponse.StatusCode != System.Net.HttpStatusCode.Accepted)
                        throw new Exception($"Error al subir el fragmento: {fragmentResponse.ReasonPhrase}");

                    totalBytesRead += bytesRead;
                }
            }
        }

        public async Task<string> CreateShareLinkAsync(string remotePath, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota no puede ser nula o vacía.", nameof(remotePath));

            await AddAuthHeaderAsync();

            var requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/createLink";
            var body = new { type = "view" };
            string jsonBody = JsonSerializer.Serialize(body);
            using var contentJson = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _http.PostAsync(requestUrl, contentJson, ct);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error al crear el enlace compartido: {response.ReasonPhrase}");

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
            string remoteConflictPath = $"lacasadelsueloradianteapp/{conflictFileName}";

            using var stream = File.OpenRead(rutaLocal);
            await UploadFileAsync(remoteConflictPath, stream);
            Debug.WriteLine($"Conflicto: se ha guardado una copia remota como {conflictFileName}");
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

        // ===================================================================
        // Métodos para la sincronización bilateral automática de imágenes
        // ===================================================================

        /// <summary>
        /// Sincroniza imágenes de manera bilateral entre el dispositivo local y OneDrive.
        /// Compara archivos por nombre y fecha de modificación y utiliza PUT y /content de la Graph API.
        /// </summary>
        public async Task SincronizarImagenesAsync(CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();
            Debug.WriteLine("Inicio de sincronización de imágenes.");

            // Verificar que el directorio local de imágenes exista
            if (!Directory.Exists(localImagesPath))
            {
                Debug.WriteLine($"El directorio local '{localImagesPath}' no existe. Creándolo.");
                Directory.CreateDirectory(localImagesPath);
            }

            // Definir extensiones de imagen permitidas
            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

            // 1. Sincronizar archivos locales: subir archivos que no existan o estén desactualizados en OneDrive.
            var localFiles = Directory.GetFiles(localImagesPath)
                                .Where(file => imageExtensions.Contains(Path.GetExtension(file)));
            foreach (var localFile in localFiles)
            {
                var fileName = Path.GetFileName(localFile);
                var remotePath = $"lacasadelsueloradianteapp/{fileName}";
                DateTime localModified = File.GetLastWriteTime(localFile);

                try
                {
                    // Obtener metadatos remotos
                    var metadataUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}";
                    var metadataResponse = await _http.GetAsync(metadataUrl, ct);
                    DateTimeOffset remoteModified = DateTimeOffset.MinValue;
                    bool remoteExists = false;

                    if (metadataResponse.IsSuccessStatusCode)
                    {
                        string metadataJson = await metadataResponse.Content.ReadAsStringAsync(ct);
                        var remoteItem = JsonSerializer.Deserialize<OneDriveItem>(metadataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
                        Debug.WriteLine($"Error al obtener metadatos del archivo remoto '{remotePath}': {metadataResponse.ReasonPhrase}");
                        continue;
                    }

                    // Comparar fechas y proceder con la sincronización
                    if (!remoteExists || localModified > remoteModified.LocalDateTime)
                    {
                        Debug.WriteLine($"Subiendo '{fileName}' a OneDrive. (Local: {localModified}, Remoto: {(remoteExists ? remoteModified.LocalDateTime.ToString() : "N/A")})");
                        using var stream = File.OpenRead(localFile);
                        await UploadFileAsync(remotePath, stream);
                    }
                    else if (remoteExists && localModified < remoteModified.LocalDateTime)
                    {
                        Debug.WriteLine($"Descargando '{fileName}' desde OneDrive. (Local: {localModified}, Remoto: {remoteModified.LocalDateTime})");
                        var downloadUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
                        var downloadResponse = await _http.GetAsync(downloadUrl, ct);
                        if (downloadResponse.IsSuccessStatusCode)
                        {
                            using var stream = await downloadResponse.Content.ReadAsStreamAsync(ct);
                            using var fileStream = File.Create(localFile);
                            await stream.CopyToAsync(fileStream, ct);
                        }
                        else
                        {
                            Debug.WriteLine($"Error al descargar '{fileName}': {downloadResponse.ReasonPhrase}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Sin cambios para '{fileName}'.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error sincronizando '{fileName}': {ex.Message}");
                }
            }

            // 2. Sincronizar archivos remotos: descargar archivos que no existan localmente o que estén actualizados.
            try
            {
                var listUrl = "https://graph.microsoft.com/v1.0/me/drive/root:/lacasadelsueloradianteapp:/children";
                var listResponse = await _http.GetAsync(listUrl, ct);
                if (listResponse.IsSuccessStatusCode)
                {
                    string listJson = await listResponse.Content.ReadAsStringAsync(ct);
                    var remoteItemsResponse = JsonSerializer.Deserialize<OneDriveItemsResponse>(listJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (remoteItemsResponse != null && remoteItemsResponse.value.Count > 0)
                    {
                        foreach (var remoteItem in remoteItemsResponse.value)
                        {
                            if (remoteItem?.name == null || !imageExtensions.Contains(Path.GetExtension(remoteItem.name)))
                                continue;

                            var localFilePath = Path.Combine(localImagesPath, remoteItem.name);
                            DateTime localModified = File.Exists(localFilePath) ? File.GetLastWriteTime(localFilePath) : DateTime.MinValue;
                            DateTimeOffset remoteModified = remoteItem.lastModifiedDateTime ?? DateTimeOffset.MinValue;

                            if (!File.Exists(localFilePath) || remoteModified.LocalDateTime > localModified)
                            {
                                Debug.WriteLine($"Descargando '{remoteItem.name}' desde OneDrive (Remoto: {remoteModified.LocalDateTime}, Local: {localModified}).");
                                var downloadUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePathFor(remoteItem.name)}:/content";
                                var downloadResponse = await _http.GetAsync(downloadUrl, ct);
                                if (downloadResponse.IsSuccessStatusCode)
                                {
                                    using var stream = await downloadResponse.Content.ReadAsStreamAsync(ct);
                                    using var fileStream = File.Create(localFilePath);
                                    await stream.CopyToAsync(fileStream, ct);
                                }
                                else
                                {
                                    Debug.WriteLine($"Error al descargar '{remoteItem.name}': {downloadResponse.ReasonPhrase}");
                                }
                            }
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"Error al listar archivos remotos: {listResponse.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error procesando archivos remotos: {ex.Message}");
            }

            Debug.WriteLine("Sincronización de imágenes finalizada.");
        }

        // Función auxiliar para formatear la ruta remota de un archivo de imagen
        private string remotePathFor(string fileName) => $"lacasadelsueloradianteapp/{fileName}";

        /// <summary>
        /// Inicia un temporizador que ejecuta la sincronización de imágenes cada 1 minuto.
        /// </summary>
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
                    Debug.WriteLine($"Error en la sincronización automática de imágenes: {ex.Message}");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            Debug.WriteLine("Temporizador de sincronización automática de imágenes iniciado.");
        }
    }

    // *******************************************************
    // Definiciones de OneDriveItem y OneDriveItemsResponse
    // *******************************************************

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