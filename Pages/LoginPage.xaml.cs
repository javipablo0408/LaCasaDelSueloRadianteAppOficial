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

                // 1) Intento Silent
                var result = await _authService.AcquireTokenSilentAsync();

                // 2) Si Silent devuelve null ? Interactive
                if (result == null)
                {
                    result = await _authService.AcquireTokenInteractiveAsync();
                }

                if (result != null)
                {
                    // Marca que ya inició sesión
                    Preferences.Default.Set("IsLoggedIn", true);

                    // Navega al AppShell (pestañas)
                    var appShell = _services.GetRequiredService<AppShell>();
                    Application.Current.MainPage = appShell;
                }
                else
                {
                    await DisplayAlert("Inicio de sesión",
                        "No se pudo obtener un token válido.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    "Error de inicio de sesión",
                    $"No se pudo iniciar sesión: {ex.Message}",
                    "OK"
                );
            }
            finally
            {
                // Restaura UI
                loginButton.IsEnabled = true;
                loadingIndicator.IsRunning = false;
                loadingIndicator.IsVisible = false;
            }
        }
    }
}
