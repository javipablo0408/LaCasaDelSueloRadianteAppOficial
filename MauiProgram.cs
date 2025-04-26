using System;
using System.Diagnostics;
using System.IO;
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
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Debug.WriteLine("[Unhandled] " + (e.ExceptionObject as Exception));
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Debug.WriteLine("[Unobserved] " + e.Exception);
                e.SetObserved();
            };

#if WINDOWS
            Microsoft.UI.Xaml.Application.Current.UnhandledException += (s, e) =>
            {
                Debug.WriteLine("[WinUI] " + e.Exception);
                e.Handled = true;
            };
#endif

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(f =>
                {
                    f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    f.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // MSAL + Graph
            builder.Services.AddSingleton<MauiMsalAuthService>();
            builder.Services.AddSingleton<OneDriveService>();

            // SQLite + Backup/Restore OneDrive
            builder.Services.AddSingleton<DatabaseService>(sp =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "clientes.db3");
                var oneDrive = sp.GetRequiredService<OneDriveService>();
                return new DatabaseService(dbPath, oneDrive);
            });

            // Páginas
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<AgregarPage>();
            builder.Services.AddSingleton<AppShell>();

            return builder.Build();
        }
    }
}