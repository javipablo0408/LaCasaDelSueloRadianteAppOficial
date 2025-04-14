using Microsoft.Graph.Models;

namespace LaCasaDelSueloRadianteApp;

public partial class LoginPage : ContentPage
{
    private GraphService _graphService;
    private User _user;

    public LoginPage()
    {
        InitializeComponent();
        _graphService = new GraphService();
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
                // Guardar información del usuario (opcional)
                Preferences.Set("UserDisplayName", _user.DisplayName);
                Preferences.Set("UserEmail", _user.UserPrincipalName);
                Preferences.Set("IsLoggedIn", true);

                // Navegar a la página principal
                await Navigation.PushAsync(new MainPage());
                Navigation.RemovePage(this);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
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