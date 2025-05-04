using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp
{
    public partial class HistorialPage : ContentPage, INotifyPropertyChanged
    {
        private readonly DatabaseService _db;

        // Propiedades para el filtrado
        private string _textoBusqueda;
        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                if (_textoBusqueda != value)
                {
                    _textoBusqueda = value;
                    OnPropertyChanged();
                    FiltrarServicios();
                }
            }
        }

        private DateTime _fechaMostrada = DateTime.Now;
        public DateTime FechaMostrada
        {
            get => _fechaMostrada;
            set
            {
                if (_fechaMostrada != value)
                {
                    _fechaMostrada = value;
                    OnPropertyChanged();
                    FiltrarServicios();
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        // Colecciones
        public ObservableCollection<Servicio> Servicios { get; } = new();
        public ObservableCollection<ServicioViewModel> ServiciosFiltrados { get; } = new();

        // Comandos
        public ICommand LimpiarBusquedaCommand { get; }
        public ICommand FechaAnteriorCommand { get; }
        public ICommand FechaSiguienteCommand { get; }
        public ICommand MostrarDetallesCommand { get; }

        public HistorialPage()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Iniciando constructor de HistorialPage");
                InitializeComponent();

                // Obtener servicios
                _db = App.Services.GetService<DatabaseService>()
                    ?? throw new InvalidOperationException("No se pudo obtener DatabaseService");

                // Inicializar comandos
                LimpiarBusquedaCommand = new Command(LimpiarBusqueda);
                FechaAnteriorCommand = new Command(MostrarFechaAnterior);
                FechaSiguienteCommand = new Command(MostrarFechaSiguiente);
                MostrarDetallesCommand = new Command<ServicioViewModel>(MostrarDetalles);

                BindingContext = this;
                System.Diagnostics.Debug.WriteLine("Constructor de HistorialPage completado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al inicializar HistorialPage: {ex.Message}\n{ex.StackTrace}");
                MainThread.BeginInvokeOnMainThread(async () =>
                    await DisplayAlert("Error", $"Error al inicializar la página: {ex.Message}", "OK"));
            }
        }

        protected override async void OnAppearing()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("HistorialPage.OnAppearing - Iniciando");
                base.OnAppearing();
                await CargarDatosAsync();
                System.Diagnostics.Debug.WriteLine("HistorialPage.OnAppearing - Completado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en OnAppearing: {ex.Message}\n{ex.StackTrace}");
                await DisplayAlert("Error", $"Error al cargar la página: {ex.Message}", "OK");
            }
        }

        private async Task CargarDatosAsync()
        {
            try
            {
                IsLoading = true;

                // Cargar servicios desde la base de datos
                var servicios = await _db.ObtenerTodosLosServiciosAsync();

                // Actualizar colección de servicios
                Servicios.Clear();
                foreach (var servicio in servicios)
                {
                    Servicios.Add(servicio);
                }

                // Filtrar servicios
                FiltrarServicios();

                IsLoading = false;
            }
            catch (SQLite.SQLiteException sqlEx)
            {
                IsLoading = false;
                System.Diagnostics.Debug.WriteLine($"Error de SQLite: {sqlEx.Message}\n{sqlEx.StackTrace}");
                await DisplayAlert("Error de base de datos", $"No se pudieron cargar los servicios: {sqlEx.Message}", "OK");
            }
            catch (Exception ex)
            {
                IsLoading = false;
                System.Diagnostics.Debug.WriteLine($"Error al cargar servicios: {ex.Message}\n{ex.StackTrace}");
                await DisplayAlert("Error", $"No se pudieron cargar los servicios: {ex.Message}", "OK");
            }
        }

        private void FiltrarServicios()
        {
            try
            {
                var consulta = Servicios.AsEnumerable();

                // Filtrar por texto de búsqueda
                if (!string.IsNullOrWhiteSpace(TextoBusqueda))
                {
                    var busqueda = TextoBusqueda.ToLowerInvariant();
                    consulta = consulta.Where(s =>
                        (!string.IsNullOrEmpty(s.TipoServicio) && s.TipoServicio.ToLowerInvariant().Contains(busqueda)) ||
                        (!string.IsNullOrEmpty(s.TipoInstalacion) && s.TipoInstalacion.ToLowerInvariant().Contains(busqueda)) ||
                        (!string.IsNullOrEmpty(s.FuenteCalor) && s.FuenteCalor.ToLowerInvariant().Contains(busqueda)));
                }
                else
                {
                    // Si no hay búsqueda, filtrar por fecha mostrada
                    consulta = consulta.Where(s => s.Fecha.Date == FechaMostrada.Date);
                }

                // Ordenar por fecha descendente (más recientes primero)
                consulta = consulta.OrderByDescending(s => s.Fecha);

                // Actualizar lista de servicios filtrados
                ServiciosFiltrados.Clear();
                foreach (var servicio in consulta)
                {
                    ServiciosFiltrados.Add(new ServicioViewModel(servicio));
                }

                System.Diagnostics.Debug.WriteLine($"Servicios filtrados: {ServiciosFiltrados.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al filtrar servicios: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void LimpiarBusqueda()
        {
            TextoBusqueda = string.Empty;
        }

        private void MostrarFechaAnterior()
        {
            FechaMostrada = FechaMostrada.AddDays(-1);
        }

        private void MostrarFechaSiguiente()
        {
            FechaMostrada = FechaMostrada.AddDays(1);
        }

        private async void MostrarDetalles(ServicioViewModel servicioVM)
        {
            if (servicioVM == null) return;

            try
            {
                // Encontrar el servicio original por ID
                var servicio = Servicios.FirstOrDefault(s => s.Id == servicioVM.Id);
                if (servicio == null)
                {
                    await DisplayAlert("Error", "No se pudo encontrar el servicio seleccionado.", "OK");
                    return;
                }

                // Obtener el cliente para este servicio
                var clientes = await _db.ObtenerClientesAsync();
                var cliente = clientes.FirstOrDefault(c => c.Id == servicio.ClienteId);

                if (cliente != null)
                {
                    // Navegar a la página de detalles
                    var imgSvc = App.Services.GetService<IImageService>();
                    await Navigation.PushAsync(new ServicioDetallePage(servicio, cliente, imgSvc));
                }
                else
                {
                    await DisplayAlert("Error", "No se pudo encontrar el cliente asociado a este servicio.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al mostrar detalles: {ex.Message}\n{ex.StackTrace}");
                await DisplayAlert("Error", $"No se pudieron cargar los detalles del servicio: {ex.Message}", "OK");
            }
        }
    }

    // Clase para el convertidor de string no vacío
    public class NotEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return !string.IsNullOrWhiteSpace(stringValue);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // La clase ServicioViewModel se mantiene igual
    public class ServicioViewModel : INotifyPropertyChanged
    {
        private readonly Servicio _servicio;

        public ServicioViewModel(Servicio servicio)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        }

        public Servicio Original => _servicio;
        public int Id => _servicio.Id;
        public int ClienteId => _servicio.ClienteId;
        public DateTime Fecha => _servicio.Fecha;
        public string TipoServicio => _servicio.TipoServicio;
        public string TipoInstalacion => _servicio.TipoInstalacion;
        public string FuenteCalor => _servicio.FuenteCalor;
        public double? ValorPh => _servicio.ValorPh;
        public double? ValorConductividad => _servicio.ValorConductividad;
        public double? ValorConcentracion => _servicio.ValorConcentracion;
        public double? ValorTurbidez => _servicio.ValorTurbidez;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}