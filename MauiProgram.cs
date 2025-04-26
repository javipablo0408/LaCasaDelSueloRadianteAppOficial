using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
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

            builder.UseMauiApp<App>()
                   .ConfigureFonts(fonts =>
                   {
                       fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                       fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                   });

            // Servicios de autenticación
            builder.Services.AddSingleton<MauiMsalAuthService>();
            builder.Services.AddSingleton<GraphService>();
            builder.Services.AddSingleton<OneDriveService>();

            // Base de datos
            builder.Services.AddSingleton<DatabaseService>(sp =>
            {
                var dbPath = Path.Combine(
                    FileSystem.AppDataDirectory,
                    "clientes.db3");
                var oneDrive = sp.GetRequiredService<OneDriveService>();
                return new DatabaseService(dbPath, oneDrive);
            });

            // Páginas
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<AgregarPage>();
            builder.Services.AddTransient<ClientesPage>();
            builder.Services.AddTransient<HistorialPage>();
            builder.Services.AddSingleton<AppShell>();

            return builder.Build();
        }
    }
}