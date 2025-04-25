using Microsoft.Identity.Client;
using System.Linq;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class MauiMsalAuthService
    {
        // -------------  IMPORTANTE -------------
        // Mantener el PCA en un campo        (Singleton = una instancia por app)
        //----------------------------------------
        private readonly IPublicClientApplication _pca;

        private readonly string[] _scopes =
        {
            "Files.ReadWrite.All",
            "User.Read"
        };

        public MauiMsalAuthService()
        {
            _pca = PublicClientApplicationBuilder
                    .Create("30af0f82-bbeb-4f49-89cd-3ff526bc339b")
#if ANDROID || IOS
                    // Esquema recomendado para apps móviles
                    .WithRedirectUri("msal30af0f82-bbeb-4f49-89cd-3ff526bc339b://auth")
#else
                    .WithRedirectUri("http://localhost")
#endif
                    .Build();

            // Si quieres persistir cache en todas las plataformas
            // TokenCacheHelper.EnableSerialization(_pca.UserTokenCache);
        }

        public async Task<AuthenticationResult> AcquireTokenAsync()
        {
            var accounts = await _pca.GetAccountsAsync();

            try
            {
                return await _pca
                    .AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                return await _pca
                    .AcquireTokenInteractive(_scopes)
                    .ExecuteAsync();
            }
        }

        public async Task SignOutAsync()
        {
            var accounts = await _pca.GetAccountsAsync();
            foreach (var account in accounts)
                await _pca.RemoveAsync(account);
        }
    }
}