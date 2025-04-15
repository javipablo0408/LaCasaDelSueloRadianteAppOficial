namespace LaCasaDelSueloRadianteApp
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        public App(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            InitializeComponent();

            Preferences.Default.Clear(); //BORRALO LUEGO 

            // Verificar si el usuario está logueado
            bool isLoggedIn = Preferences.Default.Get("IsLoggedIn", false);

            if (isLoggedIn)
            {
                MainPage = _serviceProvider.GetRequiredService<AppShell>();
            }
            else
            {
                MainPage = new NavigationPage(_serviceProvider.GetRequiredService<LoginPage>());
            }
        }
    }
}