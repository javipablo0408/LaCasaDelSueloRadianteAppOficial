using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class ServicioDetallePage : ContentPage
{
    readonly IImageService _imgSvc;
    string? _lastUrl;

    public ServicioDetallePage(Servicio servicio, Cliente cliente, IImageService imgSvc)
    {
        InitializeComponent();

        // Combinar datos del cliente y servicio en el BindingContext
        BindingContext = new
        {
            Cliente = cliente, // Pasar el cliente completo
            Servicio = servicio // Pasar el servicio completo
        };

        _imgSvc = imgSvc;
    }

    async void OnImgTapped(object? sender, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        _lastUrl = url;
        await Navigation.PushAsync(new FullScreenImagePage(url));
    }

    async void OnDownloadClicked(object sender, EventArgs e)
    {
        if (_lastUrl is null)
        {
            await DisplayAlert("Info", "Toca una imagen primero", "OK");
            return;
        }

        DownloadBar.Progress = 0;
        DownloadBar.IsVisible = true;

        var prog = new Progress<double>(p => DownloadBar.Progress = p);
        var path = await _imgSvc.DownloadAndSaveAsync(_lastUrl, prog);

        DownloadBar.IsVisible = false;

        if (path is null)
        {
            await DisplayAlert("Error", "No se pudo guardar", "OK");
        }
        else
        {
            await DisplayAlert("�xito", "Imagen guardada", "Ver");
            await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(path) });
        }
    }

    // M�todo para manejar errores de navegaci�n
    protected override bool OnBackButtonPressed()
    {
        // Puedes agregar l�gica personalizada aqu� si es necesario
        return base.OnBackButtonPressed();
    }

    // M�todo para actualizar la barra de progreso
    void UpdateProgressBar(double progress)
    {
        if (DownloadBar != null)
        {
            DownloadBar.Progress = progress;
        }
    }

    // M�todo para limpiar el estado al salir de la p�gina
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _lastUrl = null; // Limpiar la �ltima URL seleccionada
    }

    // M�todo para inicializar datos adicionales si es necesario
    void InitializeData()
    {
        // Aqu� puedes agregar l�gica para inicializar datos adicionales
        System.Diagnostics.Debug.WriteLine("Datos inicializados correctamente.");
    }

    // M�todo para manejar errores de descarga
    async Task HandleDownloadError(Exception ex)
    {
        await DisplayAlert("Error", $"Ocurri� un error durante la descarga: {ex.Message}", "OK");
    }
}