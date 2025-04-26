using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp
{
    public partial class AgregarPage : ContentPage
    {
        private readonly DatabaseService _db;
        private readonly OneDriveService _oneDrive;

        private string? _phUrl, _condUrl, _concUrl, _turbUrl;
        private const string RemoteFolder = "lacasadelsueloradianteapp";

        public AgregarPage(DatabaseService db,
                           OneDriveService oneDriveService)
        {
            InitializeComponent();
            _db = db;
            _oneDrive = oneDriveService;
        }

        /* ---------- helpers para elegir y subir foto (sin cambios) ---------- */
        private async Task<FileResult?> SeleccionarFotoAsync()
        {
            var op = await DisplayActionSheet("Fuente de la foto",
                                              "Cancelar", null,
                                              "Cámara", "Galería", "Carpeta");
            if (op == "Cancelar") return null;

            try
            {
                return op switch
                {
                    "Cámara" when MediaPicker.Default.IsCaptureSupported
                              => await MediaPicker.Default.CapturePhotoAsync(),
                    "Galería" => await MediaPicker.Default.PickPhotoAsync(),
                    "Carpeta" => await FilePicker.Default.PickAsync(new PickOptions
                    {
                        PickerTitle = "Selecciona imagen",
                        FileTypes = FilePickerFileType.Images
                    }),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error foto", ex.Message, "OK");
                return null;
            }
        }

        private async Task<string?> UploadAndShareAsync(FileResult photo,
                                                        string tipo)
        {
            var remotePath = $"{RemoteFolder}/{tipo}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(photo.FileName)}";

            await using var ms = await photo.OpenReadAsync();

            try
            {
                await _oneDrive.UploadFileAsync(remotePath, ms);
            }
            catch (InvalidOperationException ex) when (ex.Message == "LOGIN_REQUIRED")
            {
                // Token caducado → navegar a LoginPage
                await Navigation.PushAsync(
                    App.Services.GetRequiredService<LoginPage>());
                return null;
            }

            return await _oneDrive.CreateShareLinkAsync(remotePath);
        }

        /* ---------- handlers de botones ---------- */
        private async void OnAdjuntarPhFotoClicked(object s, EventArgs e) =>
            _phUrl = await SeleccionarFotoAsync() is FileResult f
                   ? await UploadAndShareAsync(f, "ph")
                   : _phUrl;

        private async void OnAdjuntarConductividadFotoClicked(object s, EventArgs e) =>
            _condUrl = await SeleccionarFotoAsync() is FileResult f
                     ? await UploadAndShareAsync(f, "conductividad")
                     : _condUrl;

        private async void OnAdjuntarConcentracionFotoClicked(object s, EventArgs e) =>
            _concUrl = await SeleccionarFotoAsync() is FileResult f
                     ? await UploadAndShareAsync(f, "concentracion")
                     : _concUrl;

        private async void OnAdjuntarTurbidezFotoClicked(object s, EventArgs e) =>
            _turbUrl = await SeleccionarFotoAsync() is FileResult f
                     ? await UploadAndShareAsync(f, "turbidez")
                     : _turbUrl;

        private async void OnGuardarClicked(object s, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NombreEntry.Text))
            {
                await DisplayAlert("Error", "Nombre requerido", "OK");
                return;
            }

            var cliente = new Cliente
            {
                NombreCliente = NombreEntry.Text!,
                Direccion = DireccionEntry.Text,
                Email = EmailEntry.Text,
                Telefono = TelefonoEntry.Text
            };
            await _db.GuardarClienteAsync(cliente);

            var servicio = new Servicio
            {
                ClienteId = cliente.Id,
                Fecha = DateTime.Now,
                TipoServicio = TipoServicioPicker.SelectedItem?.ToString(),
                TipoInstalacion = TipoInstalacionPicker.SelectedItem?.ToString(),
                FuenteCalor = FuenteCalorPicker.SelectedItem?.ToString(),
                ValorPh = double.TryParse(PhEntry.Text, out var ph) ? ph : (double?)null,
                ValorConductividad = double.TryParse(ConductividadEntry.Text, out var c) ? c : (double?)null,
                ValorConcentracion = double.TryParse(ConcentracionInhibidorEntry.Text, out var ci) ? ci : (double?)null,
                ValorTurbidez = double.TryParse(TurbidezEntry.Text, out var t) ? t : (double?)null,
                FotoPhUrl = _phUrl,
                FotoConductividadUrl = _condUrl,
                FotoConcentracionUrl = _concUrl,
                FotoTurbidezUrl = _turbUrl
            };
            await _db.GuardarServicioAsync(servicio);

            await DisplayAlert("Éxito", "Cliente y servicio guardados", "OK");
            await Navigation.PopAsync();
        }
    }
}