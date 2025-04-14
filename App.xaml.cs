namespace LaCasaDelSueloRadianteApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Verificar si el usuario ya está logueado
            bool isLoggedIn = Preferences.Get("IsLoggedIn", false);

            if (isLoggedIn)
            {
                MainPage = new NavigationPage(new LoginPage());
            }
        }
    }
}
