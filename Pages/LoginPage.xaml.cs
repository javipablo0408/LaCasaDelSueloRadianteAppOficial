using Microsoft.Graph.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Application = Microsoft.Maui.Controls.Application;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp
{
    public partial class LoginPage : ContentPage
    {
        private readonly MauiMsalAuthService _authService;
        private readonly OneDriveService _oneDriveService;

        public LoginPage()
        {
            InitializeComponent();
            _authService = new MauiMsalAuthService();
            _oneDriveService = new OneDriveService(_authService);
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            try
            {
                await _authService.AcquireTokenAsync();
                await Navigation.PushAsync(new MainPage(_oneDriveService));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error de inicio de sesión",
                                 "No se pudo iniciar sesión: " + ex.Message,
                                 "OK");
            }
        }
    }
}