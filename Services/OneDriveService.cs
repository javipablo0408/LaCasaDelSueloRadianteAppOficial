using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph.Models;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class OneDriveService
    {
        private readonly MauiMsalAuthService _auth;
        private readonly HttpClient _http;

        public class CambioLocal
        {
            public string Tipo { get; set; } // "Archivo" o "BaseDeDatos"
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
        }

        private async Task AddAuthHeaderAsync()
        {
            var authResult = await _auth.AcquireTokenAsync();
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        }

        public async Task SincronizarAsync(CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();

            // Detectar cambios locales
            var cambiosLocales = DetectarCambiosLocales();

            // Subir cambios locales a OneDrive
            foreach (var cambio in cambiosLocales)
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

            // Detectar cambios remotos
            var cambiosRemotos = await ObtenerCambiosRemotosAsync(ct);

            // Resolver conflictos y aplicar cambios remotos
            foreach (var cambioRemoto in cambiosRemotos)
            {
                if (!ExisteConflicto(cambioRemoto))
                {
                    AplicarCambioRemoto(cambioRemoto);
                }
            }

            // Registrar estado de sincronización
            RegistrarSincronizacion();
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
                    throw new Exception($"Error al descargar la imagen: {response.ReasonPhrase}");

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(localPath);
                await stream.CopyToAsync(fileStream);
            }
        }

        private List<CambioLocal> DetectarCambiosLocales()
        {
            var cambios = new List<CambioLocal>();

            // Detectar cambios en archivos locales
            var archivosLocales = Directory.GetFiles("ruta/local");
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

            // Detectar cambios en la base de datos
            var rutaBaseDeDatos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "clientes.db3");
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
            var cambios = JsonSerializer.Deserialize<List<CambioRemoto>>(json);
            return cambios ?? new List<CambioRemoto>();
        }

        private bool ExisteConflicto(CambioRemoto cambioRemoto)
        {
            var rutaLocal = Path.Combine("ruta/local", cambioRemoto.Ruta);
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
                var rutaLocal = Path.Combine("ruta/local", cambioRemoto.Ruta);
                Directory.CreateDirectory(Path.GetDirectoryName(rutaLocal)!);
                File.WriteAllText(rutaLocal, cambioRemoto.Contenido);
            }
            else if (cambioRemoto.Tipo == "BaseDeDatos")
            {
                var rutaBaseDeDatos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "clientes.db3");
                File.WriteAllText(rutaBaseDeDatos, cambioRemoto.Contenido);
            }
        }

        private void RegistrarSincronizacion()
        {
            var ultimaSincronizacion = DateTime.UtcNow;
            var rutaArchivoConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sync_state.json");
            var estadoSincronizacion = new { UltimaSincronizacion = ultimaSincronizacion };

            var json = JsonSerializer.Serialize(estadoSincronizacion, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(rutaArchivoConfig, json);
        }

        private DateTime ObtenerUltimaSincronizacion(string archivo)
        {
            var rutaArchivoConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sync_state.json");

            if (!File.Exists(rutaArchivoConfig))
                return DateTime.MinValue;

            var json = File.ReadAllText(rutaArchivoConfig);
            var estadoSincronizacion = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);

            if (estadoSincronizacion != null && estadoSincronizacion.TryGetValue(archivo, out var ultimaSincronizacion))
                return ultimaSincronizacion;

            return DateTime.MinValue;
        }

        private async Task BackupBaseDeDatosAsync()
        {
            var rutaBaseDeDatos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "clientes.db3");

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
            {
                throw new Exception($"Error al subir el archivo: {response.ReasonPhrase}");
            }
        }

        public async Task UploadLargeFileAsync(string remotePath, Stream content)
        {
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota no puede ser nula o vacía.", nameof(remotePath));

            await AddAuthHeaderAsync();

            // Crear una sesión de carga
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

            // Subir el archivo en fragmentos
            const int fragmentSize = 320 * 1024; // 320 KB
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