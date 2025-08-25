using Foundation;
using Microsoft.Identity.Client; // Necesario para MSAL
using UIKit;
using LaCasaDelSueloRadianteApp.Platforms.iOS.Services;

namespace LaCasaDelSueloRadianteApp
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp()
        {
            // Configurar el keychain para MSAL antes de crear la app
            MsalKeychainHelper.ConfigureKeychain();
            
            return MauiProgram.CreateMauiApp();
        }

        // Manejo de la redirección de autenticación MSAL
        public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
        {
            AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(url);
            return base.OpenUrl(app, url, options);
        }
    }
}