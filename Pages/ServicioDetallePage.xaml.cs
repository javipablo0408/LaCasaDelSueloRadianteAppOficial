using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class ServicioDetallePage : ContentPage
{
    readonly IImageService _imgSvc;
    string? _lastUrl;

    public ServicioDetallePage(Servicio servicio, IImageService imgSvc)
    {
        InitializeComponent();
        BindingContext = servicio;
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
            await DisplayAlert("Éxito", "Imagen guardada", "Ver");
            await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(path) });
        }
    }
}