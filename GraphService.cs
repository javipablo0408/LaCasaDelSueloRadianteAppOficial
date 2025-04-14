using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace LaCasaDelSueloRadianteApp
{
    internal class GraphService
    {
        private readonly string[] _scopes = new[] { "User.Read" };
        private const string TenantId = "0cd4c7dd-3fde-4373-8d6f-915d72ab9ce0";
        private const string ClientId = "30af0f82-bbeb-4f49-89cd-3ff526bc339b";
        private GraphServiceClient _client;

        public GraphService()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (OperatingSystem.IsWindows())
            {
                var options = new InteractiveBrowserCredentialOptions
                {
                    TenantId = TenantId,
                    ClientId = ClientId,
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                    RedirectUri = new Uri("http://localhost"),
                };

                InteractiveBrowserCredential interactiveCredential = new(options);
                _client = new GraphServiceClient(interactiveCredential, _scopes);
            }
            else
            {
                // TODO: Add iOS/Android support
            }
        }

        public async Task<User> GetMyDetailsAsync()
        {
            try
            {
                return await _client.Me.GetAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting user details: {ex.Message}", ex);
            }
        }
    }
}