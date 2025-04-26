using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
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

            // Mientras se comprueba el login, muestra una pantalla de carga
            MainPage = new ContentPage
            {
                Content = new ActivityIndicator
                {
                    IsRunning = true,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center
                }
            };

            // Llama a la inicialización asíncrona
            InitializeAppAsync();
        }

        private async void InitializeAppAsync()
        {
            try
            {
                // Comprobar si el usuario tiene un token válido
                var auth = _serviceProvider.GetRequiredService<Services.MauiMsalAuthService>();
                var token = await auth.AcquireTokenSilentAsync();

                bool estaLogueado = token != null;

                if (estaLogueado)
                {
                    MainPage = _serviceProvider.GetRequiredService<AppShell>();
                }
                else
                {
                    var login = _serviceProvider.GetRequiredService<LoginPage>();
                    MainPage = new NavigationPage(login);
                }
            }
            catch (Exception ex)
            {
                MainPage = new ContentPage
                {
                    Content = new ScrollView
                    {
                        Content = new Label
                        {
                            Text = $"Se ha producido un error al iniciar la app:\n{ex}",
                            TextColor = Colors.Red,
                            LineBreakMode = LineBreakMode.WordWrap,
                            Margin = new Thickness(20)
                        }
                    }
                };
            }
        }
    }
}