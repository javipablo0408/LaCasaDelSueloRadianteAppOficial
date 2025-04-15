using Microsoft.Maui.Storage;
using System;
using System.IO;

namespace LaCasaDelSueloRadianteApp
{
    public partial class AgregarPage : ContentPage
    {
        private readonly DatabaseService _database;

        // Variables para almacenar las rutas de las fotos
        private string _phFotoPath;
        private string _conductividadFotoPath;
        private string _concentracionFotoPath;
        private string _turbidezFotoPath;

        public AgregarPage()
        {
            InitializeComponent();

            // Inicializar la base de datos
            string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "clientes.db3");
            _database = new DatabaseService(dbPath);
        }

        private async void OnAdjuntarPhFotoClicked(object sender, EventArgs e)
        {
            var photo = await MediaPicker.PickPhotoAsync();
            if (photo != null)
            {
                _phFotoPath = Path.Combine(FileSystem.AppDataDirectory, photo.FileName);
                using (var stream = await photo.OpenReadAsync())
                using (var newStream = File.OpenWrite(_phFotoPath))
                {
                    await stream.CopyToAsync(newStream);
                }
                await DisplayAlert("Foto Adjunta", $"Foto de pH guardada en {_phFotoPath}", "OK");
            }
        }

        private async void OnAdjuntarConductividadFotoClicked(object sender, EventArgs e)
        {
            var photo = await MediaPicker.PickPhotoAsync();
            if (photo != null)
            {
                _conductividadFotoPath = Path.Combine(FileSystem.AppDataDirectory, photo.FileName);
                using (var stream = await photo.OpenReadAsync())
                using (var newStream = File.OpenWrite(_conductividadFotoPath))
                {
                    await stream.CopyToAsync(newStream);
                }
                await DisplayAlert("Foto Adjunta", $"Foto de conductividad guardada en {_conductividadFotoPath}", "OK");
            }
        }

        private async void OnAdjuntarConcentracionFotoClicked(object sender, EventArgs e)
        {
            var photo = await MediaPicker.PickPhotoAsync();
            if (photo != null)
            {
                _concentracionFotoPath = Path.Combine(FileSystem.AppDataDirectory, photo.FileName);
                using (var stream = await photo.OpenReadAsync())
                using (var newStream = File.OpenWrite(_concentracionFotoPath))
                {
                    await stream.CopyToAsync(newStream);
                }
                await DisplayAlert("Foto Adjunta", $"Foto de concentración guardada en {_concentracionFotoPath}", "OK");
            }
        }

        private async void OnAdjuntarTurbidezFotoClicked(object sender, EventArgs e)
        {
            var photo = await MediaPicker.PickPhotoAsync();
            if (photo != null)
            {
                _turbidezFotoPath = Path.Combine(FileSystem.AppDataDirectory, photo.FileName);
                using (var stream = await photo.OpenReadAsync())
                using (var newStream = File.OpenWrite(_turbidezFotoPath))
                {
                    await stream.CopyToAsync(newStream);
                }
                await DisplayAlert("Foto Adjunta", $"Foto de turbidez guardada en {_turbidezFotoPath}", "OK");
            }
        }

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            // Verificar si el cliente ya existe
            var clienteExistente = await _database.GetClienteByNameAsync(NombreEntry.Text);

            if (clienteExistente == null)
            {
                // Crear un nuevo cliente
                var nuevoCliente = new Cliente
                {
                    NombreCompleto = NombreEntry.Text,
                    Direccion = DireccionEntry.Text,
                    Email = EmailEntry.Text,
                    Telefono = TelefonoEntry.Text,
                    TipoServicio = TipoServicioPicker.SelectedItem?.ToString(),
                    TipoInstalacion = TipoInstalacionPicker.SelectedItem?.ToString(),
                    FuenteCalor = FuenteCalorPicker.SelectedItem?.ToString(),
                    Ph = PhEntry.Text,
                    PhFoto = _phFotoPath,
                    Conductividad = ConductividadEntry.Text,
                    ConductividadFoto = _conductividadFotoPath,
                    ConcentracionInhibidor = ConcentracionInhibidorEntry.Text,
                    ConcentracionInhibidorFoto = _concentracionFotoPath,
                    Turbidez = TurbidezEntry.Text,
                    TurbidezFoto = _turbidezFotoPath
                };

                await _database.SaveClienteAsync(nuevoCliente);
                await DisplayAlert("Éxito", "Cliente creado y servicio guardado.", "OK");
            }
            else
            {
                // Actualizar los datos del cliente existente
                clienteExistente.Direccion = DireccionEntry.Text;
                clienteExistente.Email = EmailEntry.Text;
                clienteExistente.Telefono = TelefonoEntry.Text;
                clienteExistente.TipoServicio = TipoServicioPicker.SelectedItem?.ToString();
                clienteExistente.TipoInstalacion = TipoInstalacionPicker.SelectedItem?.ToString();
                clienteExistente.FuenteCalor = FuenteCalorPicker.SelectedItem?.ToString();
                clienteExistente.Ph = PhEntry.Text;
                clienteExistente.PhFoto = _phFotoPath;
                clienteExistente.Conductividad = ConductividadEntry.Text;
                clienteExistente.ConductividadFoto = _conductividadFotoPath;
                clienteExistente.ConcentracionInhibidor = ConcentracionInhibidorEntry.Text;
                clienteExistente.ConcentracionInhibidorFoto = _concentracionFotoPath;
                clienteExistente.Turbidez = TurbidezEntry.Text;
                clienteExistente.TurbidezFoto = _turbidezFotoPath;

                await _database.SaveClienteAsync(clienteExistente);
                await DisplayAlert("Éxito", "Servicio guardado para el cliente existente.", "OK");
            }

            // Limpiar el formulario
            LimpiarFormulario();
        }

        private void LimpiarFormulario()
        {
            NombreEntry.Text = string.Empty;
            DireccionEntry.Text = string.Empty;
            EmailEntry.Text = string.Empty;
            TelefonoEntry.Text = string.Empty;
            TipoServicioPicker.SelectedIndex = -1;
            TipoInstalacionPicker.SelectedIndex = -1;
            FuenteCalorPicker.SelectedIndex = -1;
            PhEntry.Text = string.Empty;
            ConductividadEntry.Text = string.Empty;
            ConcentracionInhibidorEntry.Text = string.Empty;
            TurbidezEntry.Text = string.Empty;
            _phFotoPath = null;
            _conductividadFotoPath = null;
            _concentracionFotoPath = null;
            _turbidezFotoPath = null;
        }
    }
}