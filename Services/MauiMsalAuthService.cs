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
            var builder = PublicClientApplicationBuilder
                    .Create("30af0f82-bbeb-4f49-89cd-3ff526bc339b");

#if ANDROID
            builder = builder.WithRedirectUri("msal30af0f82-bbeb-4f49-89cd-3ff526bc339b://auth");
#elif IOS
            builder = builder
                .WithRedirectUri("msal30af0f82-bbeb-4f49-89cd-3ff526bc339b://auth")
                .WithIosKeychainSecurityGroup("com.lacasadelsueloradiante.app");
#else
            builder = builder.WithRedirectUri("http://localhost");
#endif

            _pca = builder.Build();
        }

        /*--------------------------------------------------------------
         * 1) Intento silencioso → AuthenticationResult? (puede ser null)
         *-------------------------------------------------------------*/
        public async Task<AuthenticationResult?> AcquireTokenSilentAsync()
        {
            try
            {
                var account = (await _pca.GetAccountsAsync()).FirstOrDefault();

                if (account == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MSAL] No hay cuentas disponibles para token silencioso");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine("[MSAL] Intentando adquirir token silencioso para cuenta existente");
                return await _pca
                    .AcquireTokenSilent(_scopes, account)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MSAL] Se requiere interacción del usuario: {ex.Message}");
                return null; // Se requiere interacción
            }
            catch (MsalException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MSAL] Error MSAL en token silencioso: {ex.ErrorCode} - {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MSAL] Error general en token silencioso: {ex.Message}");
                return null;
            }
        }

        /*--------------------------------------------------------------
         * 2) Login interactivo → AuthenticationResult
         *-------------------------------------------------------------*/
        public async Task<AuthenticationResult> AcquireTokenInteractiveAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MSAL] Iniciando adquisición de token interactivo");
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
            catch (MsalClientException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MSAL] Error de cliente MSAL: {ex.ErrorCode} - {ex.Message}");
                throw new InvalidOperationException($"Error de autenticación: {ex.Message}", ex);
            }
            catch (MsalServiceException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MSAL] Error de servicio MSAL: {ex.ErrorCode} - {ex.Message}");
                throw new InvalidOperationException($"Error del servicio de autenticación: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MSAL] Error general en token interactivo: {ex.Message}");
                throw new InvalidOperationException($"Error inesperado durante la autenticación: {ex.Message}", ex);
            }
        }

        // Elimina el método AcquireTokenAsync para evitar login interactivo automático.
        // Si lo necesitas, úsalo solo en el botón de login, nunca en el arranque de la app.
    }
}
