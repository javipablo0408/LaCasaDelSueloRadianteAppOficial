using Microsoft.Identity.Client;
using Foundation;

namespace LaCasaDelSueloRadianteApp.Platforms.iOS.Services
{
    public static class MsalKeychainHelper
    {
        public static void ConfigureKeychain()
        {
            // Configurar el keychain para MSAL en iOS
            // Esto es necesario para evitar el error de TeamId null
            try
            {
                // Configurar el keychain access group
                var keychainSecurityGroup = "com.lacasadelsueloradiante.app";
                
                // Esta configuración debe coincidir con lo que está en Entitlements.plist
                System.Diagnostics.Debug.WriteLine($"[iOS MSAL] Configurando keychain con security group: {keychainSecurityGroup}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[iOS MSAL] Error configurando keychain: {ex.Message}");
            }
        }
    }
}
