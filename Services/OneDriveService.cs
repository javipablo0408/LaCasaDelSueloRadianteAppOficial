// Services/OneDriveService.cs
using System;
using System.IO;
using System.Net;
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

        private const long SmallFileLimit = 4 * 1024 * 1024;   // 4 MiB

        public OneDriveService(MauiMsalAuthService auth)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _http = new HttpClient();
        }

        /*----------------------------------------------------*/
        /*  Cabecera Authorization – exige token SILENCIOSO    */
        /*----------------------------------------------------*/
        private async Task AddAuthHeaderAsync()
        {
            var silent = await _auth.AcquireTokenSilentAsync();
            if (silent == null)
                throw new InvalidOperationException("LOGIN_REQUIRED");

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", silent.AccessToken);
        }

        /*----------------------------------------------------*/
        /*  Descargar archivo completo                         */
        /*----------------------------------------------------*/
        public async Task<byte[]> DownloadAsync(string remotePath,
                                               CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();

            var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath.TrimStart('/')}:/content";
            var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            return await resp.Content.ReadAsByteArrayAsync(ct);
        }

        /*----------------------------------------------------*/
        /*  Subir ≤ 4 MiB – PUT /content                       */
        /*----------------------------------------------------*/
        public async Task UploadFileAsync(string remotePath,
                                          Stream content,
                                          CancellationToken ct = default)
        {
            if (content.Length > SmallFileLimit)
                throw new NotSupportedException("Use UploadLargeFileAsync para archivos > 4 MiB");

            await AddAuthHeaderAsync();

            var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath.TrimStart('/')}:/content";
            using var sc = new StreamContent(content);

            var resp = await _http.PutAsync(url, sc, ct);
            resp.EnsureSuccessStatusCode();
        }

        /*----------------------------------------------------*/
        /*  Subida por BLOQUES (cualquier tamaño)              */
        /*----------------------------------------------------*/
        public async Task UploadLargeFileAsync(string remotePath,
                                               Stream file,
                                               int chunkSize = 320 * 1024,   // 320 KiB
                                               CancellationToken ct = default)
        {
            await AddAuthHeaderAsync();

            // 1) Crear sesión de subida
            var createUrl =
                $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath.TrimStart('/')}:/createUploadSession";

            var sessionResp = await _http.PostAsync(createUrl,
                                                    new StringContent("{}"), ct);
            sessionResp.EnsureSuccessStatusCode();

            var uploadUrl = JsonDocument
                .Parse(await sessionResp.Content.ReadAsStringAsync(ct))
                .RootElement.GetProperty("uploadUrl").GetString()
                ?? throw new Exception("UploadUrl nula");

            // 2) Enviar bloques
            long total = file.Length;
            long sent = 0;
            var buffer = new byte[chunkSize];

            while (sent < total)
            {
                int read = await file.ReadAsync(buffer.AsMemory(0, chunkSize), ct);
                var rangeFrom = sent;
                var rangeTo = sent + read - 1;

                using var content = new ByteArrayContent(buffer, 0, read);
                content.Headers.ContentRange =
                    new ContentRangeHeaderValue(rangeFrom, rangeTo, total);

                var put = await _http.PutAsync(uploadUrl, content, ct);

                if (put.IsSuccessStatusCode ||
                    put.StatusCode == HttpStatusCode.Created ||
                    put.StatusCode == HttpStatusCode.Accepted)
                {
                    sent += read;
                }
                else
                {
                    throw new Exception($"Chunk upload failed: {put.ReasonPhrase}");
                }
            }
        }

        /*----------------------------------------------------*/
        /*  Enlace público (view)                              */
        /*----------------------------------------------------*/
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
                   ?? throw new Exception("Respuesta sin link.webUrl");
        }
    }
}