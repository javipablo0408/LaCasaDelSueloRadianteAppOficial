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

        /* --------- urls de fotos -------- */
        private string? _phUrl, _condUrl, _concUrl, _turbUrl;
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
         *  SELECCIÓN Y SUBIDA DE IMÁGENES
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

        private async Task<string?> SubirFotoAsync(FileResult file, string slug)
        {
            try
            {
                var remote = $"{RemoteFolder}/{slug}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";
                await using Stream stream = await file.OpenReadAsync();

                if (stream.Length <= 4 * 1024 * 1024)
                    await _oneDrive.UploadFileAsync(remote, stream);
                else
                    await _oneDrive.UploadLargeFileAsync(remote, stream);

                return await _oneDrive.CreateShareLinkAsync(remote);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo subir la foto: {ex.Message}", "OK");
                return null;
            }
        }

        /* --------- manejadores de botones foto --------- */
        private async void OnAdjuntarPhFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _phUrl = await SubirFotoAsync(f, "ph");
        }

        private async void OnAdjuntarConductividadFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _condUrl = await SubirFotoAsync(f, "conductividad");
        }

        private async void OnAdjuntarConcentracionFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _concUrl = await SubirFotoAsync(f, "concentracion");
        }

        private async void OnAdjuntarTurbidezFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _turbUrl = await SubirFotoAsync(f, "turbidez");
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
                    FotoPhUrl = _phUrl,
                    FotoConductividadUrl = _condUrl,
                    FotoConcentracionUrl = _concUrl,
                    FotoTurbidezUrl = _turbUrl
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