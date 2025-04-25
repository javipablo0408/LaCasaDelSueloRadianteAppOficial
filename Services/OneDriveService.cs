using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class OneDriveService
    {
        private readonly GraphServiceClient _graph;
        private const long SmallFileLimit = 4 * 1024 * 1024;   // 4 MiB
        private const int ChunkSize = 320 * 1024;         // 320 KiB

        public OneDriveService(MauiMsalAuthService auth)
        {
            if (auth == null) throw new ArgumentNullException(nameof(auth));
            var credential = new MsalTokenCredential(auth);
            _graph = new GraphServiceClient(
                credential,
                new[] { "Files.ReadWrite.All", "User.Read" }
            );
        }

        private RequestInformation NewRequest(
            string urlTemplate,
            Method method,
            IDictionary<string, object>? pathParams = null)
        {
            if (string.IsNullOrWhiteSpace(urlTemplate))
                throw new ArgumentException("URL template vacía.", nameof(urlTemplate));

            var info = new RequestInformation
            {
                HttpMethod = method,
                UrlTemplate = urlTemplate
            };
            if (pathParams != null)
                foreach (var kv in pathParams)
                    info.PathParameters.Add(kv.Key, kv.Value);
            return info;
        }

        public async Task<IList<DriveItem>> ListAsync(
            string? folderPath = null,
            CancellationToken ct = default)
        {
            var tpl = string.IsNullOrEmpty(folderPath)
                ? "{+baseurl}/me/drive/root/children"
                : "{+baseurl}/me/drive/root:/{folderPath}:/children";

            var p = string.IsNullOrEmpty(folderPath)
                ? null
                : new Dictionary<string, object> { ["folderPath"] = folderPath.TrimStart('/') };

            var req = NewRequest(tpl, Method.GET, p);
            var resp = await _graph.RequestAdapter
                                   .SendAsync<DriveItemCollectionResponse>(
                                       req,
                                       DriveItemCollectionResponse.CreateFromDiscriminatorValue,
                                       cancellationToken: ct)
                                   .ConfigureAwait(false);

            if (resp?.Value == null)
                return new List<DriveItem>();
            return resp.Value is IList<DriveItem> list
                ? list
                : resp.Value.ToList();
        }

        public async Task<DriveItem> UploadFileAsync(
            string remotePath,
            Stream content,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
                throw new ArgumentException("Ruta remota vacía.", nameof(remotePath));
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            var clean = remotePath.TrimStart('/');
            // Carga pequeña
            if (content.Length <= SmallFileLimit)
            {
                var putReq = NewRequest(
                    "{+baseurl}/me/drive/root:/{remotePath}:/content",
                    Method.PUT,
                    new Dictionary<string, object> { ["remotePath"] = clean }
                );
                putReq.SetStreamContent(content);
                return await _graph.RequestAdapter
                                   .SendAsync<DriveItem>(
                                       putReq,
                                       DriveItem.CreateFromDiscriminatorValue,
                                       cancellationToken: ct)
                                   .ConfigureAwait(false);
            }

            // Carga grande
            var sesReq = NewRequest(
                "{+baseurl}/me/drive/root:/{remotePath}:/createUploadSession",
                Method.POST,
                new Dictionary<string, object> { ["remotePath"] = clean }
            );
            var session = await _graph.RequestAdapter
                                      .SendAsync<UploadSession>(
                                          sesReq,
                                          UploadSession.CreateFromDiscriminatorValue,
                                          cancellationToken: ct)
                                      .ConfigureAwait(false);

            var uploader = new LargeFileUploadTask<DriveItem>(
                session,
                content,
                ChunkSize,
                _graph.RequestAdapter   // ← adapter en 4º parámetro
            );
            var result = await uploader.UploadAsync(cancellationToken: ct)
                                       .ConfigureAwait(false);
            if (result.UploadSucceeded && result.ItemResponse != null)
                return result.ItemResponse;

            throw new IOException("La carga fragmentada falló.");
        }

        public async Task<byte[]> DownloadAsync(
            string remotePath,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
                throw new ArgumentException("Ruta remota vacía.", nameof(remotePath));

            var clean = remotePath.TrimStart('/');
            var req = NewRequest(
                "{+baseurl}/me/drive/root:/{remotePath}:/content",
                Method.GET,
                new Dictionary<string, object> { ["remotePath"] = clean }
            );
            var stream = await _graph.RequestAdapter
                                     .SendPrimitiveAsync<Stream>(
                                         req,
                                         errorMapping: null,
                                         cancellationToken: ct)
                                     .ConfigureAwait(false);

            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            return ms.ToArray();
        }

        public async Task DeleteAsync(string remotePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
                throw new ArgumentException("Ruta remota vacía.", nameof(remotePath));

            var clean = remotePath.TrimStart('/');
            var req = NewRequest(
                "{+baseurl}/me/drive/root:/{remotePath}:",
                Method.DELETE,
                new Dictionary<string, object> { ["remotePath"] = clean }
            );
            await _graph.RequestAdapter
                        .SendNoContentAsync(req, cancellationToken: ct)
                        .ConfigureAwait(false);
        }

        public async Task<DriveItem> CreateFolderAsync(
            string parentFolderPath,
            string folderName,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(parentFolderPath))
                throw new ArgumentException("Ruta padre vacía.", nameof(parentFolderPath));
            if (string.IsNullOrWhiteSpace(folderName))
                throw new ArgumentException("Nombre de carpeta vacío.", nameof(folderName));

            var cleanParent = parentFolderPath.TrimStart('/');
            var req = NewRequest(
                "{+baseurl}/me/drive/root:/{parentFolderPath}:/children",
                Method.POST,
                new Dictionary<string, object> { ["parentFolderPath"] = cleanParent }
            );

            var body = new DriveItem
            {
                Name = folderName,
                Folder = new Folder()
            };

            // ← PASAMOS el REQUEST ADAPTER, no la fábrica de serialización
            req.SetContentFromParsable(
                _graph.RequestAdapter,
                "application/json",
                body
            );

            return await _graph.RequestAdapter
                               .SendAsync<DriveItem>(
                                   req,
                                   DriveItem.CreateFromDiscriminatorValue,
                                   cancellationToken: ct)
                               .ConfigureAwait(false);
        }
    }
}