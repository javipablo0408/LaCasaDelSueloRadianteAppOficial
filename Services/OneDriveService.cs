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

        /*--------------------------------------------------
         *  Cabecera Authorization (solo si hay token válido)
         *-------------------------------------------------*/
        private async Task AddAuthHeaderAsync()
        {
            var silent = await _auth.AcquireTokenSilentAsync();
            if (silent == null)
                throw new InvalidOperationException("LOGIN_REQUIRED");

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", silent.AccessToken);
        }

        /*--------------------------------------------------
         *  Descargar
         *-------------------------------------------------*/
        public async Task<byte[]> DownloadAsync(string remotePath,
                                               CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();
            var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath.TrimStart('/')}:/content";

            var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }

        /*--------------------------------------------------
         *  Subir (≤ 4 MiB)
         *-------------------------------------------------*/
        public async Task UploadFileAsync(string remotePath,
                                          Stream content,
                                          CancellationToken ct = default)
        {
            if (content.Length > SmallFileLimit)
                throw new NotSupportedException("Solo se permiten archivos ≤ 4 MiB");

            await AddAuthHeaderAsync();
            var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath.TrimStart('/')}:/content";

            using var sc = new StreamContent(content);
            var resp = await _http.PutAsync(url, sc, ct);
            resp.EnsureSuccessStatusCode();
        }

        /*--------------------------------------------------
         *  Crear enlace público “view”
         *-------------------------------------------------*/
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

            using var js = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(js, cancellationToken: ct);
            return doc.RootElement.GetProperty("link")
                                  .GetProperty("webUrl")
                                  .GetString()
                   ?? throw new Exception("No se devolvió link.webUrl");
        }
    }
}