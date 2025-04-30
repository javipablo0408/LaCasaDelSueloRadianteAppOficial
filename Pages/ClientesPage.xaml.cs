using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class ClientesPage : ContentPage
{
    private readonly DatabaseService _db;

    public ObservableCollection<Cliente> Clientes { get; set; } = new();
    public ICommand EditarCommand { get; }
    public ICommand EliminarCommand { get; }

    public ClientesPage()
    {
        InitializeComponent();

        // Recuperar DatabaseService desde el contenedor de servicios
        _db = App.Services.GetService<DatabaseService>()
              ?? throw new InvalidOperationException("No se pudo obtener la instancia de DatabaseService.");

        // Comando para editar un cliente
        EditarCommand = new Command<Cliente>(async (cliente) =>
        {
            if (cliente != null)
            {
                // Navegar a la página de edición
                await Navigation.PushAsync(new EditarClientePage(cliente, _db, Clientes));
            }
        });

        // Comando para eliminar un cliente
        EliminarCommand = new Command<Cliente>(async (cliente) =>
        {
            if (cliente != null)
            {
                bool confirm = await DisplayAlert("Confirmar", $"¿Deseas eliminar a {cliente.NombreCliente}?", "Sí", "No");
                if (confirm)
                {
                    // Eliminar de la base de datos
                    await _db.EliminarClienteAsync(cliente);
                    // Eliminar de la lista
                    Clientes.Remove(cliente);
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

            ClientesCollectionView.ItemsSource = Clientes;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error al cargar los clientes: {ex.Message}", "OK");
        }
    }
}