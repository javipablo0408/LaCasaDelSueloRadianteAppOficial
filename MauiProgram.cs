using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()                 // registra App
                .ConfigureFonts(f =>
                {
                    f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    f.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            /* ---------- Servicios ---------- */
            builder.Services.AddSingleton<MauiMsalAuthService>();
            builder.Services.AddSingleton<OneDriveService>();

            // DatabaseService necesita el OneDriveService y la ruta del .db3
            builder.Services.AddSingleton<DatabaseService>(sp =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "clientes.db3");
                var oneDrive = sp.GetRequiredService<OneDriveService>();
                return new DatabaseService(dbPath, oneDrive);
            });

            /* ---------- Páginas ---------- */
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<AgregarPage>();
            builder.Services.AddSingleton<AppShell>();

            /* ---------- Build & export provider ---------- */
            var mauiApp = builder.Build();
            App.Services = mauiApp.Services;      // ←  exposición global
            return mauiApp;
        }
    }
}