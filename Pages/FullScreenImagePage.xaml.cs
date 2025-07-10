using Microsoft.Maui.Storage;
using System.IO;

namespace LaCasaDelSueloRadianteApp;

public partial class FullScreenImagePage : ContentPage
{
    double _scale = 1;

    public FullScreenImagePage(string fileName)
    {
        InitializeComponent();

        // Construye la ruta completa a la imagen local
        var fullPath = Path.Combine(FileSystem.AppDataDirectory, fileName);
        MainImage.Source = ImageSource.FromFile(fullPath);
    }

    void OnTap(object sender, TappedEventArgs e) => Navigation.PopAsync();

    void OnPinch(object sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Running)
        {
            _scale += (e.Scale - 1);
            _scale = Math.Clamp(_scale, 1, 4);
            MainImage.Scale = _scale;
        }
    }
}