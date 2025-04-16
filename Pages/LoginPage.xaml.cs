using Microsoft.Maui.Controls;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp
{
    public partial class LoginPage : ContentPage
    {
        private readonly MauiMsalAuthService _authService;
        private readonly OneDriveService _oneDriveService;
        private readonly IServiceProvider _serviceProvider;

        public LoginPage(
            MauiMsalAuthService authService,
            OneDriveService oneDriveService,
            IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _authService = authService;
            _oneDriveService = oneDriveService;
            _serviceProvider = serviceProvider;
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            try
            {
                // Deshabilitar el bot�n de inicio de sesi�n y mostrar el indicador de carga
                loginButton.IsEnabled = false;
                loadingIndicator.IsVisible = true;
                loadingIndicator.IsRunning = true;

                // Adquirir el token
                var result = await _authService.AcquireTokenAsync();
                if (result != null)
                {
                    // Obtener AppShell del contenedor de servicios
                    var appShell = _serviceProvider.GetRequiredService<AppShell>();
                    Application.Current.MainPage = appShell;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error de inicio de sesi�n",
                    "No se pudo iniciar sesi�n: " + ex.Message, "OK");
            }
            finally
            {
                // Habilitar el bot�n de inicio de sesi�n y ocultar el indicador de carga
                loginButton.IsEnabled = true;
                loadingIndicator.IsRunning = false;
                loadingIndicator.IsVisible = false;
            }
        }
    }
}