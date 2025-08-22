using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class ClientesPage : ContentPage
{
    private readonly DatabaseService _db;

    public ObservableCollection<Cliente> Clientes { get; set; } = new();
    public ObservableCollection<Cliente> ClientesFiltrados { get; set; } = new();
    public string FiltroBusqueda { get; set; }
    public ICommand EditarCommand { get; }
    public ICommand EliminarCommand { get; }
    public ICommand NavegarAServicioCommand { get; }

    public ClientesPage()
    {
        InitializeComponent();

        // Recuperar DatabaseService desde el contenedor de servicios
        _db = App.Services.GetService<DatabaseService>()
              ?? throw new InvalidOperationException("No se pudo obtener la instancia de DatabaseService.");

        // Comando para navegar a ServicioPage
        NavegarAServicioCommand = new Command<Cliente>(async (cliente) =>
        {
            if (cliente != null)
            {
                await Navigation.PushAsync(new ServiciosPage(cliente));
            }
        });

        // Comando para editar un cliente
        EditarCommand = new Command<Cliente>(async (cliente) =>
        {
            if (cliente != null)
            {
                await Navigation.PushAsync(new EditarClientePage(cliente, _db, Clientes));
            }
        });

        // Comando para eliminar un cliente y sus servicios asociados
        EliminarCommand = new Command<Cliente>(async (cliente) =>
        {
            if (cliente != null)
            {
                bool confirm = await DisplayAlert("Confirmar", $"�Deseas eliminar a {cliente.NombreCliente} y todos sus servicios asociados?", "S�", "No");
                if (confirm)
                {
                    // 1. Obtener los servicios asociados al cliente
                    var servicios = await _db.ObtenerServiciosAsync(cliente.Id);

                    // 2. Eliminar cada servicio asociado
                    foreach (var servicio in servicios)
                    {
                        await _db.EliminarServicioAsync(servicio);
                    }

                    // 3. Eliminar el cliente
                    await _db.EliminarClienteAsync(cliente);

                    // 4. Subir la cola de sincronizaci�n a OneDrive (actualiza el JSON)
                    await _db.SubirSyncQueueAsync();

                    // 5. Actualizar la lista en la UI
                    Clientes.Remove(cliente);
                    FiltrarClientes();
                }
            }
        });

        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarClientesAsync();
    }

    private async Task CargarClientesAsync()
    {
        try
        {
            var clientes = await _db.ObtenerClientesAsync();
            Clientes.Clear();
            foreach (var cliente in clientes)
            {
                Clientes.Add(cliente);
            }

            FiltrarClientes();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error al cargar los clientes: {ex.Message}", "OK");
        }
    }

    private void FiltrarClientes()
    {
        if (string.IsNullOrWhiteSpace(FiltroBusqueda))
        {
            ClientesFiltrados.Clear();
            foreach (var cliente in Clientes)
            {
                ClientesFiltrados.Add(cliente);
            }
        }
        else
        {
            var textoBusqueda = FiltroBusqueda.ToLower();
            var resultados = Clientes.Where(c =>
                (!string.IsNullOrEmpty(c.NombreCliente) && c.NombreCliente.ToLower().Contains(textoBusqueda)) ||
                (!string.IsNullOrEmpty(c.Direccion) && c.Direccion.ToLower().Contains(textoBusqueda)) ||
                (!string.IsNullOrEmpty(c.Telefono) && c.Telefono.ToLower().Contains(textoBusqueda)));

            ClientesFiltrados.Clear();
            foreach (var cliente in resultados)
            {
                ClientesFiltrados.Add(cliente);
            }
        }

        ClientesCollectionView.ItemsSource = ClientesFiltrados;
    }

    private void OnSearchBarTextChanged(object sender, TextChangedEventArgs e)
    {
        FiltroBusqueda = e.NewTextValue;
        FiltrarClientes();
    }
}