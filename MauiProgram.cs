using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Services;
using System.IO;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Syncfusion.Licensing;
using Microsoft.Maui.Networking;
using LaCasaDelSueloRadianteApp.Pages;

namespace LaCasaDelSueloRadianteApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        SyncfusionLicenseProvider.RegisterLicense("Mzk1MTk3NUAzMzMwMmUzMDJlMzAzYjMzMzAzYk82dkJFN0E3d1V0a3lGSE1oVlFIb3daK3R2M0tYSDIrK1Bicitwakc1Lzg9");



        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(f =>
            {
                f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                f.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Servicios
        builder.Services.AddSingleton<MauiMsalAuthService>();
        builder.Services.AddHttpClient<OneDriveService>();
        builder.Services.AddSingleton<IImageService, ImageService>();
        builder.Services.AddSingleton<DatabaseService>(sp =>
        {
            var oneDrive = sp.GetRequiredService<OneDriveService>();
            var logger = sp.GetRequiredService<ILogger<DatabaseService>>();
            return new DatabaseService(oneDrive, logger);
        });

        // Páginas
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<HistorialPage>();
        builder.Services.AddTransient<AgregarPage>();
        builder.Services.AddTransient<ServicioDetallePage>();
        builder.Services.AddSingleton<AppShell>();

        var app = builder.Build();
        App.Services = app.Services;

        
        System.Diagnostics.Debug.WriteLine("MauiProgram: App inicializada. La sincronización se manejará desde App.xaml.cs usando SyncManagerService.");

        return app;
    }
}