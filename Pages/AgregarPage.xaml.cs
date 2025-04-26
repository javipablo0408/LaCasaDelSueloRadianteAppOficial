using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Services;
using System.Diagnostics;

namespace LaCasaDelSueloRadianteApp
{
    public partial class AgregarPage : ContentPage
    {
        private readonly DatabaseService _db;
        private readonly OneDriveService _oneDrive;
        private string? _phUrl, _condUrl, _concUrl, _turbUrl;
        private const string RemoteFolder = "lacasadelsueloradianteapp";

        public AgregarPage(DatabaseService db, OneDriveService oneDriveService)
        {
            try
            {
                InitializeComponent();
                _db = db ?? throw new ArgumentNullException(nameof(db));
                _oneDrive = oneDriveService ?? throw new ArgumentNullException(nameof(oneDriveService));

                // Inicializar pickers
                TipoServicioPicker.ItemsSource = new[]
                {
                    "Mantenimiento",
                    "Puesta en marcha"
                };

                TipoInstalacionPicker.ItemsSource = new[]
                {
                    "Suelo radiante",
                    "Radiadores"
                };

                FuenteCalorPicker.ItemsSource = new[]
                {
                    "Caldera gas",
                    "Biomasa",
                    "Bomba calor"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en constructor AgregarPage: {ex}");
                throw;
            }
        }

        private async Task<FileResult?> SeleccionarFotoAsync()
        {
            try
            {
                var op = await DisplayActionSheet(
                    "Fuente de la foto",
                    "Cancelar", null,
                    "Cámara", "Galería", "Archivos");

                if (op == "Cancelar") return null;

                return op switch
                {
                    "Cámara" when MediaPicker.Default.IsCaptureSupported
                        => await MediaPicker.Default.CapturePhotoAsync(),

                    "Galería"
                        => await MediaPicker.Default.PickPhotoAsync(),

                    "Archivos"
                        => await FilePicker.Default.PickAsync(new PickOptions
                        {
                            PickerTitle = "Selecciona imagen",
                            FileTypes = FilePickerFileType.Images
                        }),

                    _ => null
                };
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    $"No se pudo obtener la foto: {ex.Message}", "OK");
                return null;
            }
        }

        private async Task<string?> UploadAndShareAsync(FileResult photo,
                                                      string tipo)
        {
            try
            {
                var remotePath = $"{RemoteFolder}/{tipo}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(photo.FileName)}";

                await using var stream = await photo.OpenReadAsync();

                if (stream.Length <= 4 * 1024 * 1024)
                    await _oneDrive.UploadFileAsync(remotePath, stream);
                else
                    await _oneDrive.UploadLargeFileAsync(remotePath, stream);

                return await _oneDrive.CreateShareLinkAsync(remotePath);
            }
            catch (InvalidOperationException ex) when (ex.Message == "LOGIN_REQUIRED")
            {
                await Navigation.PushAsync(
                    App.Services.GetRequiredService<LoginPage>());
                return null;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    $"No se pudo subir la foto: {ex.Message}", "OK");
                return null;
            }
        }

        private async void OnAdjuntarPhFotoClicked(object s, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _phUrl = await UploadAndShareAsync(f, "ph");
        }

        private async void OnAdjuntarConductividadFotoClicked(object s, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _condUrl = await UploadAndShareAsync(f, "conductividad");
        }

        private async void OnAdjuntarConcentracionFotoClicked(object s, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _concUrl = await UploadAndShareAsync(f, "concentracion");
        }

        private async void OnAdjuntarTurbidezFotoClicked(object s, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _turbUrl = await UploadAndShareAsync(f, "turbidez");
        }

        private async void OnGuardarClicked(object s, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(NombreEntry.Text))
                {
                    await DisplayAlert("Error",
                        "El nombre del cliente es obligatorio", "OK");
                    return;
                }

                // Guardar cliente
                var cliente = new Cliente
                {
                    NombreCliente = NombreEntry.Text!.Trim(),
                    Direccion = DireccionEntry.Text?.Trim(),
                    Email = EmailEntry.Text?.Trim(),
                    Telefono = TelefonoEntry.Text?.Trim()
                };
                await _db.GuardarClienteAsync(cliente);

                // Guardar servicio
                var servicio = new Servicio
                {
                    ClienteId = cliente.Id,
                    Fecha = DateTime.Now,
                    TipoServicio = TipoServicioPicker.SelectedItem?.ToString(),
                    TipoInstalacion = TipoInstalacionPicker.SelectedItem?.ToString(),
                    FuenteCalor = FuenteCalorPicker.SelectedItem?.ToString(),

                    ValorPh = double.TryParse(PhEntry.Text, out var ph)
                        ? ph : null,
                    ValorConductividad = double.TryParse(ConductividadEntry.Text, out var c)
                        ? c : null,
                    ValorConcentracion = double.TryParse(ConcentracionInhibidorEntry.Text, out var ci)
                        ? ci : null,
                    ValorTurbidez = double.TryParse(TurbidezEntry.Text, out var t)
                        ? t : null,

                    FotoPhUrl = _phUrl,
                    FotoConductividadUrl = _condUrl,
                    FotoConcentracionUrl = _concUrl,
                    FotoTurbidezUrl = _turbUrl
                };
                await _db.GuardarServicioAsync(servicio);

                await DisplayAlert("Éxito",
                    "Cliente y servicio guardados correctamente", "OK");
                await Navigation.PopAsync();
            }
            catch (InvalidOperationException ex) when (ex.Message == "LOGIN_REQUIRED")
            {
                await Navigation.PushAsync(
                    App.Services.GetRequiredService<LoginPage>());
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    $"No se pudo guardar: {ex.Message}", "OK");
            }
        }
    }
}