using Microsoft.Graph.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Application = Microsoft.Maui.Controls.Application;

namespace LaCasaDelSueloRadianteApp
{
    public partial class LoginPage : ContentPage
    {
        private readonly GraphService _graphService;
        private readonly IServiceProvider _serviceProvider;
        private User? _user;

        public LoginPage(GraphService graphService, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            try
            {
                // Mostrar indicador de carga
                LoginButton.IsEnabled = false;
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;

                // Intentar obtener los detalles del usuario
                _user = await _graphService.GetMyDetailsAsync();

                if (_user != null)
                {
                    // Guardar información del usuario
                    Preferences.Default.Set("UserDisplayName", _user.DisplayName);
                    Preferences.Default.Set("UserEmail", _user.UserPrincipalName);
                    Preferences.Default.Set("IsLoggedIn", true);

                    // Navegar a la página principal
                    Application.Current!.MainPage = _serviceProvider.GetRequiredService<AppShell>();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error de inicio de sesión",
                    $"No se pudo iniciar sesión: {ex.Message}", "OK");
            }
            finally
            {
                // Ocultar indicador de carga
                LoginButton.IsEnabled = true;
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
            }
        }
    }
}