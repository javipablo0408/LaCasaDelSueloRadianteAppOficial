using Microsoft.Identity.Client;
using System.Linq;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class MauiMsalAuthService
    {
        private readonly IPublicClientApplication _pca;
        private readonly string[] _scopes = new string[]
        {
            "Files.ReadWrite.All",
            "User.Read"
        };

        public MauiMsalAuthService()
        {
            _pca = PublicClientApplicationBuilder.Create("30af0f82-bbeb-4f49-89cd-3ff526bc339b")
                .WithRedirectUri("http://localhost")
                .Build();
        }

        public async Task<AuthenticationResult> AcquireTokenAsync()
        {
            var accounts = await _pca.GetAccountsAsync();
            try
            {
                return await _pca.AcquireTokenSilent(_scopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                return await _pca.AcquireTokenInteractive(_scopes).ExecuteAsync();
            }
        }

        public async Task SignOutAsync()
        {
            var accounts = await _pca.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await _pca.RemoveAsync(account);
            }
        }
    }
}