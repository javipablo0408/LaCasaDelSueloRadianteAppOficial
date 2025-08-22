using System;
using System.Globalization;
using System.IO;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp.Converters
{
    public class LocalImagePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string fileName && !string.IsNullOrWhiteSpace(fileName))
            {
                // Construye la ruta completa en la carpeta de imágenes
                var fullPath = Path.Combine(AppPaths.ImagesPath, fileName);
                return ImageSource.FromFile(fullPath);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}