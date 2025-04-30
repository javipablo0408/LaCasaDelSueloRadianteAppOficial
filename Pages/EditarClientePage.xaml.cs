using System.Collections.ObjectModel;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class EditarClientePage : ContentPage
{
    private readonly DatabaseService _dbService;
    private readonly ObservableCollection<Cliente> _clientes;

    public Cliente Cliente { get; set; }
    public Command GuardarCommand { get; }

    public EditarClientePage(Cliente cliente, DatabaseService dbService, ObservableCollection<Cliente> clientes)
    {
        InitializeComponent();

        Cliente = cliente;
        _dbService = dbService;
        _clientes = clientes;

        // Comando para guardar los cambios
        GuardarCommand = new Command(async () =>
        {
            // Actualizar en la base de datos
            await _dbService.ActualizarClienteAsync(Cliente);

            // Actualizar en la lista
            var clienteExistente = _clientes.FirstOrDefault(c => c.Id == Cliente.Id);
            if (clienteExistente != null)
            {
                clienteExistente.NombreCliente = Cliente.NombreCliente;
                clienteExistente.Email = Cliente.Email;
                clienteExistente.Direccion = Cliente.Direccion;
            }

            await DisplayAlert("Éxito", "Cliente actualizado correctamente.", "OK");
            await Navigation.PopAsync();
        });

        BindingContext = this;
    }
}