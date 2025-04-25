using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
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
                // UI: deshabilita botón y muestra spinner
                loginButton.IsEnabled = false;
                loadingIndicator.IsVisible = true;
                loadingIndicator.IsRunning = true;

                // Solicita / recupera token
                var result = await _authService.AcquireTokenAsync();

                if (result != null)
                {
                    // Navega al AppShell registrado en DI
                    var appShell = _services.GetRequiredService<AppShell>();
                    Application.Current.MainPage = appShell;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error de inicio de sesión",
                                   $"No se pudo iniciar sesión: {ex.Message}",
                                   "OK");
            }
            finally
            {
                // Restablece UI
                loginButton.IsEnabled = true;
                loadingIndicator.IsRunning = false;
                loadingIndicator.IsVisible = false;
            }
        }
    }
}