using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Linq;
using LaCasaDelSueloRadianteApp.Services;
using LaCasaDelSueloRadianteApp.Models;

namespace LaCasaDelSueloRadianteApp.Pages;

public partial class HistorialPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly IImageService _imageService;

    public HistorialPage(DatabaseService db, IImageService imageService)
    {
        InitializeComponent();
        _db = db;
        _imageService = imageService;
        BindingContext = new HistorialPageViewModel(_db, _imageService, this);
    }

    // Evento para navegar al detalle del servicio seleccionado
    public async void ServiciosCollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is HistorialItem itemSeleccionado)
        {
            ServiciosCollectionView.SelectedItem = null;

            var servicio = await _db.ObtenerTodosLosServiciosAsync();
            var servicioSeleccionado = servicio.FirstOrDefault(s => s.Id == itemSeleccionado.ServicioId);
            var cliente = await _db.ObtenerClientePorIdAsync(itemSeleccionado.ClienteId);
            var instalador = await _db.ObtenerInstaladorAsync();

            if (servicioSeleccionado == null)
            {
                await DisplayAlert("Error", "No se encontró el servicio.", "OK");
                return;
            }
            if (cliente == null)
            {
                await DisplayAlert("Error", $"No se encontró el cliente para este servicio (ClienteId: {itemSeleccionado.ClienteId}).", "OK");
                return;
            }
            if (instalador == null)
            {
                await DisplayAlert("Error", "No se encontró un instalador en la base de datos.", "OK");
                return;
            }
            if (_imageService == null)
            {
                await DisplayAlert("Error", "No se pudo obtener el servicio de imágenes.", "OK");
                return;
            }

            await Navigation.PushAsync(
                new ServicioDetallePage(servicioSeleccionado, cliente, instalador, _imageService)
            );
        }
    }
}

// Clase auxiliar para mostrar datos del cliente en la lista
public class HistorialItem
{
    public int ServicioId { get; set; }
    public int ClienteId { get; set; }
    public string NombreCliente { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string Telefono { get; set; } = "";
}

public class HistorialPageViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _db;
    private readonly IImageService _imageService;
    private readonly Page _page;

    public ObservableCollection<Servicio> Servicios { get; set; } = new();
    public ObservableCollection<HistorialItem> ServiciosFiltrados { get; set; } = new();

    private List<Cliente> _clientes = new();

    private string _textoBusqueda = string.Empty;
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set
        {
            if (_textoBusqueda != value)
            {
                _textoBusqueda = value;
                OnPropertyChanged();
                FiltrarServiciosPorFecha();
            }
        }
    }

    private DateTime _fechaSeleccionada = DateTime.Today;
    public DateTime FechaSeleccionada
    {
        get => _fechaSeleccionada;
        set
        {
            if (_fechaSeleccionada != value)
            {
                _fechaSeleccionada = value;
                OnPropertyChanged();
                FiltrarServiciosPorFecha();
                OnPropertyChanged(nameof(PuedeRetroceder));
                OnPropertyChanged(nameof(PuedeAvanzar));
            }
        }
    }

    public DateTime FechaMinima { get; set; } = DateTime.Today.AddYears(-5);
    public DateTime FechaMaxima { get; set; } = DateTime.Today.AddYears(1);

    public bool PuedeRetroceder => FechaSeleccionada > FechaMinima;
    public bool PuedeAvanzar => FechaSeleccionada < FechaMaxima;

    public ICommand DiaAnteriorCommand { get; }
    public ICommand DiaSiguienteCommand { get; }

    public HistorialPageViewModel(DatabaseService db, IImageService imageService, Page page)
    {
        _db = db;
        _imageService = imageService;
        _page = page;
        DiaAnteriorCommand = new Command(() =>
        {
            if (PuedeRetroceder)
                FechaSeleccionada = FechaSeleccionada.AddDays(-1);
        });
        DiaSiguienteCommand = new Command(() =>
        {
            if (PuedeAvanzar)
                FechaSeleccionada = FechaSeleccionada.AddDays(1);
        });
        CargarServicios();
    }

    private async void CargarServicios()
    {
        var servicios = await _db.ObtenerTodosLosServiciosAsync();
        var clientes = await _db.ObtenerClientesAsync();
        Servicios.Clear();
        _clientes = clientes;
        foreach (var servicio in servicios)
            Servicios.Add(servicio);

        if (Servicios.Any())
        {
            FechaMinima = Servicios.Min(s => s.Fecha).Date;
            FechaMaxima = Servicios.Max(s => s.Fecha).Date;
            FechaSeleccionada = FechaMaxima;
            OnPropertyChanged(nameof(FechaMinima));
            OnPropertyChanged(nameof(FechaMaxima));
            OnPropertyChanged(nameof(PuedeRetroceder));
            OnPropertyChanged(nameof(PuedeAvanzar));
        }
        FiltrarServiciosPorFecha();
    }

    private void FiltrarServiciosPorFecha()
    {
        var texto = TextoBusqueda?.ToLowerInvariant() ?? string.Empty;

        var filtrados = Servicios
            .Where(s => s.Fecha.Date == FechaSeleccionada.Date)
            .Select(s =>
            {
                var cliente = _clientes.FirstOrDefault(c => c != null && c.Id == s.ClienteId);
                return new HistorialItem
                {
                    ServicioId = s.Id,
                    ClienteId = s.ClienteId,
                    NombreCliente = cliente?.NombreCliente ?? "",
                    Direccion = cliente?.Direccion ?? "",
                    Telefono = cliente?.Telefono ?? ""
                };
            })
            .Where(item =>
                string.IsNullOrWhiteSpace(texto) ||
                (item.NombreCliente?.ToLowerInvariant().Contains(texto) ?? false) ||
                (item.Direccion?.ToLowerInvariant().Contains(texto) ?? false) ||
                (item.Telefono?.ToLowerInvariant().Contains(texto) ?? false)
            )
            .ToList();

        ServiciosFiltrados.Clear();
        foreach (var item in filtrados)
            ServiciosFiltrados.Add(item);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}