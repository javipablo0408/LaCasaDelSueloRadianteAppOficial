using Microsoft.Identity.Client;
using System.Linq;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class MauiMsalAuthService
    {
        // ---------- configuración ----------
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
                    .WithRedirectUri("msal30af0f82-bbeb-4f49-89cd-3ff526bc339b://auth")
#else
                    .WithRedirectUri("http://localhost")
#endif
                    .Build();
        }

        /*──────────────────────────────────────────────────────────────*/
        /* 1) Intento SILENCIOSO                                        */
        /*──────────────────────────────────────────────────────────────*/
        public async Task<AuthenticationResult?> AcquireTokenSilentAsync()
        {
            var acc = (await _pca.GetAccountsAsync()).FirstOrDefault();
            try
            {
                return await _pca
                    .AcquireTokenSilent(_scopes, acc)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                return null; // se necesita UI
            }
        }

        /*──────────────────────────────────────────────────────────────*/
        /* 2) Login INTERACTIVO                                         */
        /*──────────────────────────────────────────────────────────────*/
        public async Task<AuthenticationResult> AcquireTokenInteractiveAsync()
        {
            return await _pca
                .AcquireTokenInteractive(_scopes)
                .ExecuteAsync();
        }

        /*──────────────────────────────────────────────────────────────*/
        /* 3) Wrapper COMPATIBLE con código existente                   */
        /*    - 1º intenta silent                                       */
        /*    - si null → hace interactive                              */
        /*──────────────────────────────────────────────────────────────*/
        public async Task<AuthenticationResult> AcquireTokenAsync()
        {
            var silent = await AcquireTokenSilentAsync();
            return silent ?? await AcquireTokenInteractiveAsync();
        }
    }
}