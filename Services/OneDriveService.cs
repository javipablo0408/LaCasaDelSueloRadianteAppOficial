// Services/OneDriveService.cs
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp.Services
{
    /// <summary>
    /// Operaciones mínimas con OneDrive:
    ///  • DownloadAsync   – descarga un archivo como byte[]
    ///  • UploadFileAsync – sube (PUT) archivos hasta 4 MiB
    ///  • CreateShareLinkAsync – genera URL pública “view”
    /// 
    ///  *Todo funciona vía REST sin usar request-builders de Graph.*
    /// </summary>
    public class OneDriveService
    {
        private readonly MauiMsalAuthService _auth;
        private readonly HttpClient _http;
        private const long SmallFileLimit = 4 * 1024 * 1024; // 4 MiB

        public OneDriveService(MauiMsalAuthService auth)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _http = new HttpClient();
        }

        /*--------------------------------------------------------------
         *  Helpers
         *-------------------------------------------------------------*/
        private async Task<string> GetAccessTokenAsync()
        {
            var result = await _auth.AcquireTokenAsync();
            return result.AccessToken;
        }

        private async Task AddAuthHeaderAsync()
        {
            var token = await GetAccessTokenAsync();
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        /*--------------------------------------------------------------
         *  Descargar: GET /content
         *-------------------------------------------------------------*/
        public async Task<byte[]> DownloadAsync(string remotePath,
                                               CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();
            var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath.TrimStart('/')}:/content";

            var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }

        /*--------------------------------------------------------------
         *  Subir (hasta 4 MiB): PUT /content
         *  Para DB grandes (>4 MiB) bastará trocear o aumentar SmallFileLimit.
         *-------------------------------------------------------------*/
        public async Task UploadFileAsync(string remotePath,
                                          Stream content,
                                          CancellationToken ct = default)
        {
            if (content.Length > SmallFileLimit)
                throw new NotSupportedException(
                    "Esta implementación simple admite archivos ≤ 4 MiB");

            await AddAuthHeaderAsync();
            var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath.TrimStart('/')}:/content";

            using var sc = new StreamContent(content);
            var resp = await _http.PutAsync(url, sc, ct);
            resp.EnsureSuccessStatusCode();
        }

        /*--------------------------------------------------------------
         *  Crear enlace público “view”
         *-------------------------------------------------------------*/
        public async Task<string> CreateShareLinkAsync(string remotePath,
                                                       string type = "view",
                                                       string scope = "anonymous",
                                                       CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();
            var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath.TrimStart('/')}:/createLink";

            var body = JsonSerializer.Serialize(new { type, scope });
            using var content = new StringContent(body);
            content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json");

            var resp = await _http.PostAsync(url, content, ct);
            resp.EnsureSuccessStatusCode();

            using var respStream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(respStream, cancellationToken: ct);
            return doc.RootElement
                      .GetProperty("link")
                      .GetProperty("webUrl")
                      .GetString()
                ?? throw new Exception("No se devolvió link.webUrl");
        }
    }
}