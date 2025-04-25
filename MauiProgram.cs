using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // 1) Capturar excepciones .NET globales
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Debug.WriteLine("[UnhandledException] " +
                    (e.ExceptionObject as Exception)?.ToString());

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Debug.WriteLine("[UnobservedTaskException] " + e.Exception);
                e.SetObserved();
            };

#if WINDOWS
            // 2) Capturar excepciones WinUI
            Microsoft.UI.Xaml.Application.Current.UnhandledException += (s, e) =>
            {
                Debug.WriteLine("[WinUI UnhandledException] " + e.Exception);
                e.Handled = true;
            };
#endif

            // 3) Crear el builder de MAUI
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // 4) Inyección de dependencias
            builder.Services.AddSingleton<MauiMsalAuthService>();
            builder.Services.AddSingleton<OneDriveService>();

            // Registro de DatabaseService con la ruta física de la base de datos
            builder.Services.AddSingleton<DatabaseService>(sp =>
            {
                // Construye la ruta de la BD en el directorio privado de la app
                var dbPath = Path.Combine(
                    FileSystem.AppDataDirectory,
                    "clientes.db3"
                );
                return new DatabaseService(dbPath);
            });

            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<AgregarPage>();
            builder.Services.AddSingleton<AppShell>();

            return builder.Build();
        }
    }
}