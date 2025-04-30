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
        /* --------- dependencias --------- */
        private readonly DatabaseService _db;
        private readonly OneDriveService _oneDrive;

        /* --------- rutas locales y remotas -------- */
        private string? _phLocalPath, _condLocalPath, _concLocalPath, _turbLocalPath;
        private const string RemoteFolder = "lacasadelsueloradianteapp";

        public AgregarPage(DatabaseService db, OneDriveService oneDrive)
        {
            InitializeComponent();

            _db = db ?? throw new ArgumentNullException(nameof(db));
            _oneDrive = oneDrive ?? throw new ArgumentNullException(nameof(oneDrive));

            /* llenar pickers */
            TipoServicioPicker.ItemsSource = new[] { "Mantenimiento", "Puesta en marcha" };
            TipoInstalacionPicker.ItemsSource = new[] { "Suelo radiante", "Radiadores" };
            FuenteCalorPicker.ItemsSource = new[] { "Caldera gas", "Biomasa", "Bomba calor" };
        }

        /* =======================================================
         *  SELECCIÓN, GUARDADO LOCAL Y SUBIDA DE IMÁGENES
         * =======================================================*/
        private async Task<FileResult?> SeleccionarFotoAsync()
        {
            var accion = await DisplayActionSheet("Seleccionar foto", "Cancelar", null,
                                                  "Cámara", "Galería", "Archivos");
            if (accion == "Cancelar") return null;

            try
            {
                return accion switch
                {
                    "Cámara" when MediaPicker.Default.IsCaptureSupported
                        => await MediaPicker.Default.CapturePhotoAsync(),

                    "Galería"
                        => await MediaPicker.Default.PickPhotoAsync(),

                    "Archivos"
                        => await FilePicker.Default.PickAsync(new PickOptions
                        {
                            PickerTitle = "Selecciona una imagen",
                            FileTypes = FilePickerFileType.Images
                        }),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo obtener la foto: {ex.Message}", "OK");
                return null;
            }
        }

        private async Task<string?> GuardarYSubirFotoAsync(FileResult file, string slug)
        {
            try
            {
                // Guardar imagen localmente
                var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var localPath = Path.Combine(localFolder, $"{slug}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}");

                await using (var localStream = File.Create(localPath))
                {
                    await using var fileStream = await file.OpenReadAsync();
                    await fileStream.CopyToAsync(localStream);
                }

                // Subir imagen a OneDrive
                var remotePath = $"{RemoteFolder}/{Path.GetFileName(localPath)}";
                await using var uploadStream = File.OpenRead(localPath);
                await _oneDrive.UploadFileAsync(remotePath, uploadStream);

                return localPath; // Retornar la ruta local
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo guardar o subir la foto: {ex.Message}", "OK");
                return null;
            }
        }

        /* --------- manejadores de botones foto --------- */
        private async void OnAdjuntarPhFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _phLocalPath = await GuardarYSubirFotoAsync(f, "ph");
        }

        private async void OnAdjuntarConductividadFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _condLocalPath = await GuardarYSubirFotoAsync(f, "conductividad");
        }

        private async void OnAdjuntarConcentracionFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _concLocalPath = await GuardarYSubirFotoAsync(f, "concentracion");
        }

        private async void OnAdjuntarTurbidezFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _turbLocalPath = await GuardarYSubirFotoAsync(f, "turbidez");
        }

        /* =======================================================
         *  GUARDAR CLIENTE + SERVICIO
         * =======================================================*/
        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NombreEntry.Text))
            {
                await DisplayAlert("Error", "El nombre del cliente es obligatorio", "OK");
                return;
            }

            try
            {
                /* 1) Cliente */
                var cliente = new Cliente
                {
                    NombreCliente = NombreEntry.Text!.Trim(),
                    Direccion = DireccionEntry.Text?.Trim(),
                    Email = EmailEntry.Text?.Trim(),
                    Telefono = TelefonoEntry.Text?.Trim()
                };
                await _db.GuardarClienteAsync(cliente);

                /* 2) Servicio */
                var servicio = new Servicio
                {
                    ClienteId = cliente.Id,
                    Fecha = DateTime.Now,
                    TipoServicio = TipoServicioPicker.SelectedItem?.ToString(),
                    TipoInstalacion = TipoInstalacionPicker.SelectedItem?.ToString(),
                    FuenteCalor = FuenteCalorPicker.SelectedItem?.ToString(),
                    ValorPh = double.TryParse(PhEntry.Text, out var ph) ? ph : null,
                    ValorConductividad = double.TryParse(ConductividadEntry.Text, out var c) ? c : null,
                    ValorConcentracion = double.TryParse(ConcentracionInhibidorEntry.Text, out var ci) ? ci : null,
                    ValorTurbidez = double.TryParse(TurbidezEntry.Text, out var t) ? t : null,
                    FotoPhUrl = _phLocalPath,
                    FotoConductividadUrl = _condLocalPath,
                    FotoConcentracionUrl = _concLocalPath,
                    FotoTurbidezUrl = _turbLocalPath
                };
                await _db.GuardarServicioAsync(servicio);

                await DisplayAlert("Éxito", "Datos guardados correctamente", "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo guardar: {ex.Message}", "OK");
            }
        }
    }
}