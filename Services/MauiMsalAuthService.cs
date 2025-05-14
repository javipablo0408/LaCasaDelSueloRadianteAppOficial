using Microsoft.Identity.Client;
using System.Linq;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class MauiMsalAuthService
    {
        /*--------------------------------------------------------------
         *  Configuración MSAL
         *-------------------------------------------------------------*/
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

        /*--------------------------------------------------------------
         * 1) Intento silencioso → AuthenticationResult? (puede ser null)
         *-------------------------------------------------------------*/
        public async Task<AuthenticationResult?> AcquireTokenSilentAsync()
        {
            var account = (await _pca.GetAccountsAsync()).FirstOrDefault();

            try
            {
                return await _pca
                    .AcquireTokenSilent(_scopes, account)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                return null; // Se requiere interacción
            }
        }

        /*--------------------------------------------------------------
         * 2) Login interactivo → AuthenticationResult
         *-------------------------------------------------------------*/
        public async Task<AuthenticationResult> AcquireTokenInteractiveAsync()
        {
#if ANDROID
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            return await _pca
                .AcquireTokenInteractive(_scopes)
                .WithParentActivityOrWindow(activity) // Especifica la actividad actual
                .ExecuteAsync();
#else
            return await _pca
                .AcquireTokenInteractive(_scopes)
                .ExecuteAsync();
#endif
        }

        /*--------------------------------------------------------------
         * 3) Wrapper de compatibilidad (silent → interactive)
         *-------------------------------------------------------------*/
        public async Task<AuthenticationResult> AcquireTokenAsync()
        {
            var silent = await AcquireTokenSilentAsync();
            return silent ?? await AcquireTokenInteractiveAsync();
        }
    }
}