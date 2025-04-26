using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LaCasaDelSueloRadianteApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace LaCasaDelSueloRadianteApp
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;
        public static IServiceProvider Services { get; private set; } = default!;

        public App(IServiceProvider serviceProvider)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Debug.WriteLine("[Unhandled] " + (e.ExceptionObject as Exception));

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Debug.WriteLine("[Unobserved] " + e.Exception);
                e.SetObserved();
            };

#if WINDOWS
            Microsoft.UI.Xaml.Application.Current.UnhandledException += (_, e) =>
            {
                Debug.WriteLine("[WinUI] " + e.Exception);
                e.Handled = true;
            };
#endif
            _serviceProvider = serviceProvider;
            Services = serviceProvider;

            InitializeComponent();

            // Inicializar la app de forma asíncrona
            MainThread.BeginInvokeOnMainThread(async () => await InitializeAsync());
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Inicializar la base de datos
                var db = _serviceProvider.GetRequiredService<DatabaseService>();
                await db.InitAsync();

                // Comprobar login
                var auth = _serviceProvider.GetRequiredService<Services.MauiMsalAuthService>();
                var token = await auth.AcquireTokenSilentAsync();

                if (token != null)
                {
                    MainPage = _serviceProvider.GetRequiredService<AppShell>();
                }
                else
                {
                    MainPage = new NavigationPage(
                        _serviceProvider.GetRequiredService<LoginPage>());
                }
            }
            catch (Exception ex)
            {
                MainPage = new ContentPage
                {
                    Content = new ScrollView
                    {
                        Content = new StackLayout
                        {
                            Spacing = 10,
                            Padding = new Thickness(20),
                            Children =
                            {
                                new Label
                                {
                                    Text = "Error al iniciar la app:",
                                    FontAttributes = FontAttributes.Bold
                                },
                                new Label
                                {
                                    Text = ex.Message,
                                    TextColor = Colors.Red
                                },
                                new Button
                                {
                                    Text = "Reintentar",
                                    Command = new Command(async () =>
                                        await InitializeAsync())
                                }
                            }
                        }
                    }
                };
            }
        }
    }
}