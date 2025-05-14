using Microsoft.Maui.Storage;
using System.IO;

namespace LaCasaDelSueloRadianteApp
{
    public static class AppPaths
    {
        // Carpeta base de la app (siempre con permisos de escritura)
        public static string BasePath => FileSystem.AppDataDirectory;

        // Ruta completa a la base de datos
        public static string DatabasePath => Path.Combine(BasePath, "clientes.db3");

        // Carpeta para imágenes (subcarpeta dentro de la base)
        public static string ImagesPath => Path.Combine(BasePath, "Images");

        // Carpeta para backups temporales (opcional, útil para backups en caliente)
        public static string TempPath => Path.Combine(BasePath, "Temp");

        /// <summary>
        /// Crea los directorios necesarios si no existen.
        /// Llama a este método al inicio de la app (por ejemplo, en MauiProgram.cs).
        /// </summary>
        public static void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(BasePath);
            Directory.CreateDirectory(ImagesPath);
            Directory.CreateDirectory(TempPath);
        }
    }
}