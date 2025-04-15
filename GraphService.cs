using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Drives.Item.Items.Item.CreateLink;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp
{
    public class GraphServiceConfiguration
    {
        public string TenantId { get; init; } = string.Empty;
        public string ClientId { get; init; } = string.Empty;
        public string[] Scopes { get; init; } = new[] {
            "User.Read",
            "Files.ReadWrite",
            "Files.ReadWrite.All"
        };
        public string RedirectUri { get; init; } = "http://localhost";
    }

    public class GraphService : IDisposable
    {
        private readonly GraphServiceConfiguration _config;
        private GraphServiceClient? _client;
        private bool _disposed;

        public GraphService(GraphServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            InitializeClient();
        }

        private void InitializeClient()
        {
            var options = new InteractiveBrowserCredentialOptions
            {
                TenantId = _config.TenantId,
                ClientId = _config.ClientId,
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                RedirectUri = new Uri(_config.RedirectUri),
            };

            var credential = new InteractiveBrowserCredential(options);
            _client = new GraphServiceClient(credential, _config.Scopes);
        }

        public async Task<User?> GetMyDetailsAsync(CancellationToken cancellationToken = default)
        {
            if (_client == null) throw new InvalidOperationException("Graph client not initialized.");

            try
            {
                return await _client.Me.GetAsync(cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving user details: {ex.Message}");
                throw;
            }
        }

        public async Task<(string ItemId, string ShareUrl)> UploadFileToOneDriveAsync(
            string filePath,
            string originalFileName,
            CancellationToken cancellationToken = default)
        {
            if (_client == null) throw new InvalidOperationException("Graph client not initialized.");
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found.", filePath);

            try
            {
                // Crear el nombre del archivo con timestamp
                var timestampedFileName = $"{Path.GetFileNameWithoutExtension(originalFileName)}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(originalFileName)}";

                // Subir el archivo directamente a OneDrive
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

                // Obtener la referencia al drive del usuario
                var drive = await _client.Me.Drive.GetAsync(cancellationToken: cancellationToken);
                if (drive == null) throw new Exception("Could not access OneDrive");

                // Subir el archivo
                var uploadedItem = await _client.Drives[drive.Id]
                    .Items["root"]
                    .ItemWithPath(timestampedFileName)
                    .Content
                    .PutAsync(fileStream, cancellationToken: cancellationToken);

                if (uploadedItem == null)
                    throw new Exception("Failed to upload file");

                // Crear enlace compartido
                var requestBody = new CreateLinkPostRequestBody
                {
                    Type = "view",
                    Scope = "anonymous"
                };

                var sharedItem = await _client.Drives[drive.Id]
                    .Items[uploadedItem.Id]
                    .CreateLink
                    .PostAsync(requestBody, cancellationToken: cancellationToken);

                return (uploadedItem.Id!, sharedItem?.Link?.WebUrl ?? string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading file: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                (_client as IDisposable)?.Dispose();
            }

            _disposed = true;
        }

        ~GraphService()
        {
            Dispose(false);
        }
    }
}