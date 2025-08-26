using Microsoft.Identity.Client;
using System.Linq;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class MauiMsalAuthService_Backup
    {
        /*--------------------------------------------------------------
         *  Configuración MSAL SIMPLE - VERSION DE RESPALDO
         *-------------------------------------------------------------*/
        private readonly IPublicClientApplication _pca;

        private readonly string[] _scopes =
        {
            "Files.ReadWrite.All",
            "User.Read"
        };

        public MauiMsalAuthService_Backup()
        {
            // Configuración ultra-simple que funcionaba
            _pca = PublicClientApplicationBuilder
                    .Create("30af0f82-bbeb-4f49-89cd-3ff526bc339b")
#if ANDROID || IOS
                    .WithRedirectUri("msal30af0f82-bbeb-4f49-89cd-3ff526bc339b://auth")
#else
                    .WithRedirectUri("http://localhost")
#endif
                    .Build();
        }

        public async Task<AuthenticationResult?> AcquireTokenSilentAsync()
        {
            try
            {
                var account = (await _pca.GetAccountsAsync()).FirstOrDefault();
                if (account == null) return null;

                return await _pca
                    .AcquireTokenSilent(_scopes, account)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                return null; // Se requiere interacción
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<AuthenticationResult> AcquireTokenInteractiveAsync()
        {
            try
            {
#if ANDROID
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                return await _pca
                    .AcquireTokenInteractive(_scopes)
                    .WithParentActivityOrWindow(activity)
                    .ExecuteAsync();
#else
                return await _pca
                    .AcquireTokenInteractive(_scopes)
                    .ExecuteAsync();
#endif
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error durante la autenticación: {ex.Message}", ex);
            }
        }
    }
}
