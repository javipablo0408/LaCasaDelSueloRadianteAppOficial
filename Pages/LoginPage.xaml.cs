using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;              // Para Preferences
using LaCasaDelSueloRadianteApp.Services;
using System;

namespace LaCasaDelSueloRadianteApp
{
    public partial class LoginPage : ContentPage
    {
        private readonly MauiMsalAuthService _authService;
        private readonly IServiceProvider _services;

        public LoginPage(MauiMsalAuthService authService,
                         IServiceProvider services)
        {
            InitializeComponent();
            _authService = authService;
            _services = services;
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            try
            {
                // Deshabilita UI mientras se autentica...
                loginButton.IsEnabled = false;
                loadingIndicator.IsVisible = true;
                loadingIndicator.IsRunning = true;

                System.Diagnostics.Debug.WriteLine("[LOGIN] Iniciando proceso de login");

                // 1) Intento Silent
                System.Diagnostics.Debug.WriteLine("[LOGIN] Intentando login silencioso");
                var result = await _authService.AcquireTokenSilentAsync();

                // 2) Si Silent devuelve null → Interactive
                if (result == null)
                {
                    System.Diagnostics.Debug.WriteLine("[LOGIN] Login silencioso falló, intentando login interactivo");
                    result = await _authService.AcquireTokenInteractiveAsync();
                }

                if (result != null)
                {
                    System.Diagnostics.Debug.WriteLine("[LOGIN] Login exitoso");
                    // Marca que ya inició sesión
                    Preferences.Default.Set("IsLoggedIn", true);

                    // Navega al AppShell (pestañas)
                    var appShell = _services.GetRequiredService<AppShell>();
                    Application.Current!.MainPage = appShell;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[LOGIN] Login falló - resultado null");
                    await DisplayAlert("Inicio de sesión",
                        "No se pudo obtener un token válido.",
                        "OK");
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Error de operación: {ex.Message}");
                await DisplayAlert(
                    "Error de inicio de sesión",
                    ex.Message,
                    "OK"
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Error general: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[LOGIN] StackTrace: {ex.StackTrace}");
                await DisplayAlert(
                    "Error de inicio de sesión",
                    $"Error inesperado: {ex.Message}",
                    "OK"
                );
            }
            finally
            {
                // Restaura UI
                loginButton.IsEnabled = true;
                loadingIndicator.IsRunning = false;
                loadingIndicator.IsVisible = false;
                System.Diagnostics.Debug.WriteLine("[LOGIN] Proceso de login finalizado");
            }
        }
    }
}
