using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.IO;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp.Controls
{
    public partial class MiniImg : ContentView
    {
        public static readonly BindableProperty UrlProperty =
            BindableProperty.Create(
                nameof(Url),
                typeof(string),
                typeof(MiniImg),
                default(string),
                propertyChanged: OnUrlChanged);

        public string Url
        {
            get => (string)GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }

        private static async void OnUrlChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (MiniImg)bindable;
            var fileName = newValue as string;

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                try
                {
                    var fullPath = Path.Combine(AppPaths.ImagesPath, fileName);
                    if (File.Exists(fullPath))
                    {
                        control._image.Source = ImageSource.FromFile(fullPath);
                    }
                    else
                    {
                        control._image.Source = null;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading image in MiniImg: {ex.Message}");
                    control._image.Source = null;
                }
            }
            else
            {
                control._image.Source = null;
            }
        }

        private void HandleTap(object sender, EventArgs e)
        {
            // Si quieres propagar el evento, puedes hacerlo aqu�
            if (!string.IsNullOrWhiteSpace(Url))
                Tapped?.Invoke(this, Url);
        }

        private readonly Image _image;

        public MiniImg()
        {
            _image = new Image
            {
                Aspect = Aspect.AspectFill,
                HeightRequest = 100,
                WidthRequest = 100
            };
            Content = _image;

            // Si tienes eventos de tap, agr�galos aqu�
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(Url))
                    Tapped?.Invoke(this, Url);
            };
            _image.GestureRecognizers.Add(tapGesture);
        }

        public event EventHandler<string> Tapped;
    }
}