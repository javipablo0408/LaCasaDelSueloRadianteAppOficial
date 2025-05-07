using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using Microsoft.Maui.Storage;

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

        public async Task SincronizarBidireccionalAsync(CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();

            try
            {
                var cambiosLocales = DetectarCambiosLocales();
                var cambiosRemotos = await ObtenerCambiosRemotosAsync(ct);

                foreach (var cambio in cambiosLocales)
                {
                    var nombreArchivo = Path.GetFileName(cambio.RutaLocal);
                    var remoteCambio = cambiosRemotos.FirstOrDefault(cr => cr.Ruta != null && cr.Ruta.Equals(nombreArchivo, StringComparison.OrdinalIgnoreCase));

                    if (remoteCambio != null)
                    {
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
                        }
                        else if (fechaLocal < remoteCambio.FechaModificacion)
                        {
                            if (ConflictResolutionStrategy == ConflictStrategy.CloudPriority || (!ExisteConflicto(remoteCambio)))
                            {
                                AplicarCambioRemoto(remoteCambio);
                            }
                        }
                    }
                    else
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
                }

                foreach (var cambioRemoto in cambiosRemotos)
                {
                    var rutaLocal = Path.Combine(RutaBaseLocal, cambioRemoto.Ruta);
                    if (!File.Exists(rutaLocal))
                    {
                        AplicarCambioRemoto(cambioRemoto);
                    }
                }

                var remoteFileNames = cambiosRemotos
                    .Where(cr => cr.Ruta != null)
                    .Select(cr => cr.Ruta)
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
                        System.Diagnostics.Debug.WriteLine($"Archivo re-subido al detectar eliminación remota: {fileName}");
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
                        System.Diagnostics.Debug.WriteLine($"Archivo no encontrado en OneDrive: {remotePath}");
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
                        RutaRemota = $"ruta/remota/{infoArchivo.Name}"
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
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var delta = JsonSerializer.Deserialize<DeltaResponse>(json, options);
                var cambios = delta?.value ?? new List<CambioRemoto>();

                // Detectar elementos eliminados
                foreach (var item in cambios)
                {
                    if (item.Tipo == null && item.Ruta == null) // Elemento eliminado
                    {
                        item.Tipo = "Eliminado";
                    }
                }

                return cambios;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al deserializar el JSON: {ex.Message}");
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

            using var stream = File.OpenRead(rutaBaseDeDatos);
            var rutaRemota = "lacasadelsueloradianteapp/clientes_backup.db3";
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
    }
}