using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Services;
using Microsoft.Maui.Networking;

namespace LaCasaDelSueloRadianteApp
{
    public partial class AgregarPage : ContentPage, INotifyPropertyChanged
    {
        private readonly DatabaseService _db;

        // Fotos analíticas
        private string? _phLocalPath, _condLocalPath, _concLocalPath, _turbLocalPath;
        // Fotos de instalación
        private readonly string?[] _fotosInstalacion = new string?[10];
        private bool _dbInitialized = false;

        // Cliente seleccionado para autorelleno
        private Cliente? _clienteSeleccionado = null;

        // Servicio en edición (si aplica)
        private Servicio? _servicioEditando = null;

        // Vista previa de imágenes adjuntas
        public ObservableCollection<string> ImagenesAdjuntas { get; } = new();

        // Estado de guardado
        private bool _isGuardando;
        public bool IsGuardando
        {
            get => _isGuardando;
            set
            {
                if (_isGuardando != value)
                {
                    _isGuardando = value;
                    OnPropertyChanged(nameof(IsGuardando));
                }
            }
        }
        // Información de campos
        private async void OnInfoNombreClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Introduce el nombre completo del cliente.", "OK");
        }
        private async void OnInfoDireccionClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Introduce la dirección donde se realiza el servicio.", "OK");
        }
        private async void OnInfoEmailClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Introduce el correo electrónico del cliente para notificaciones o contacto.", "OK");
        }
        private async void OnInfoTelefonoClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Introduce el número de teléfono del cliente.", "OK");
        }
        private async void OnInfoTipoServicioClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Selecciona el tipo de servicio realizado. Si no aparece en la lista, selecciona 'Otros' y especifícalo.", "OK");
        }
        private async void OnInfoTipoInstalacionClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Selecciona el tipo de instalación. Si no aparece en la lista, selecciona 'Otros' y especifícalo.", "OK");
        }
        private async void OnInfoFuenteCalorClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Selecciona la fuente de calor principal de la instalación.", "OK");
        }
        private async void OnInfoAntiguedadInstalacionClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Indica los años de antigüedad de la instalación.", "OK");
        }
        private async void OnInfoAntiguedadAparatoClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Indica los años de antigüedad del aparato de producción de calor.", "OK");
        }
        private async void OnInfoMarcaClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Introduce la marca del aparato de producción de calor.", "OK");
        }
        private async void OnInfoModeloClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Introduce el modelo del aparato de producción de calor.", "OK");
        }
        private async void OnInfoUltimaRevisionClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Indica hace cuántos años se realizó la última revisión.", "OK");
        }
        private async void OnInfoProximaVisitaClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Selecciona en cuántos meses se recomienda la próxima visita.", "OK");
        }
        private async void OnInfoEquipamientoClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Selecciona el equipamiento utilizado durante el servicio.", "OK");
        }
        private async void OnInfoPhClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Introduce el valor de pH medido en la instalación.", "OK");
        }
        private async void OnInfoConductividadClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Introduce el valor de conductividad medido en la instalación.", "OK");
        }
        private async void OnInfoConcentracionClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Introduce la concentración de inhibidor medida.", "OK");
        }
        private async void OnInfoTurbidezClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Selecciona el nivel de turbidez del agua de la instalación.", "OK");
        }
        private async void OnInfoComentariosClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Añade cualquier comentario relevante sobre el servicio.", "OK");
        }
        private async void OnInfoComentariosInstaladorClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Añade comentarios internos del instalador, si es necesario.", "OK");
        }
        private async void OnInfoProductosUtilizadosClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Indica los productos utilizados durante el servicio, por categoría.", "OK");
        }
        private async void OnInfoInhibidoresClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Selecciona el inhibidor utilizado y la cantidad aplicada.", "OK");
        }
        private async void OnInfoLimpiadoresClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Selecciona el limpiador utilizado y la cantidad aplicada.", "OK");
        }
        private async void OnInfoBiocidasClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Selecciona el biocida utilizado y la cantidad aplicada.", "OK");
        }
        private async void OnInfoAnticongelantesClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Información", "Selecciona el anticongelante utilizado y la cantidad aplicada.", "OK");
        }
        public AgregarPage(DatabaseService db)
        {
            InitializeComponent();

            _db = db ?? throw new ArgumentNullException(nameof(db));

            TipoServicioPicker.ItemsSource = new[] { "Test calidad agua", "Revisión periódica", "Tratamiento / limpieza", "Puesta en marcha / Aditivar", "Otros" };
            TipoInstalacionPicker.ItemsSource = new[] { "Suelo radiante", "Radiadores", "Fancoil", "Otros" };
            FuenteCalorPicker.ItemsSource = new[] { "Caldera gas", "Caldera gasoil", "Aerotermia", "Biomasa", "Geotermia", "Otros" };
            ProximaVisitaPicker.ItemsSource = new[] { "6 meses", "12 meses", "24 meses" };

            BindingContext = this;
        }

        // Constructor para modo edición
        public AgregarPage(DatabaseService db, Servicio servicioEditar, Cliente? clienteEditar = null)
            : this(db)
        {
            _servicioEditando = servicioEditar;
            PrecargarDatos(servicioEditar, clienteEditar);
            Title = "Editar servicio";
        }

        private void PrecargarDatos(Servicio servicio, Cliente? cliente)
        {
            if (cliente != null)
            {
                NombreEntry.Text = cliente.NombreCliente;
                DireccionEntry.Text = cliente.Direccion;
                EmailEntry.Text = cliente.Email;
                TelefonoEntry.Text = cliente.Telefono;
                _clienteSeleccionado = cliente;
            }

            TipoServicioPicker.SelectedItem = ObtenerValorPicker(TipoServicioPicker, servicio.TipoServicio);
            TipoServicioOtroEntry.Text = servicio.TipoServicio;
            TipoServicioOtroEntry.IsVisible = !TipoServicioPicker.ItemsSource.Cast<string>().Contains(servicio.TipoServicio);

            TipoInstalacionPicker.SelectedItem = ObtenerValorPicker(TipoInstalacionPicker, servicio.TipoInstalacion);
            TipoInstalacionOtroEntry.Text = servicio.TipoInstalacion;
            TipoInstalacionOtroEntry.IsVisible = !TipoInstalacionPicker.ItemsSource.Cast<string>().Contains(servicio.TipoInstalacion);

            FuenteCalorPicker.SelectedItem = ObtenerValorPicker(FuenteCalorPicker, servicio.FuenteCalor);
            FuenteCalorOtroEntry.Text = servicio.FuenteCalor;
            FuenteCalorOtroEntry.IsVisible = !FuenteCalorPicker.ItemsSource.Cast<string>().Contains(servicio.FuenteCalor);

            AntiguedadInstalacionEntry.Text = servicio.AntiguedadInstalacion?.ToString();
            AntiguedadAparatoProduccionEntry.Text = servicio.AntiguedadAparatoProduccion?.ToString();
            MarcaEntry.Text = servicio.Marca;
            ModeloEntry.Text = servicio.Modelo;
            UltimaRevisionEntry.Text = servicio.UltimaRevision?.ToString();

            ProximaVisitaPicker.SelectedItem = servicio.ProximaVisita;
            EquipamientoPicker.SelectedItem = ObtenerValorPicker(EquipamientoPicker, servicio.EquipamientoUtilizado);

            _phLocalPath = servicio.FotoPhUrl;
            _condLocalPath = servicio.FotoConductividadUrl;
            _concLocalPath = servicio.FotoConcentracionUrl;
            _turbLocalPath = servicio.FotoTurbidezUrl;

            _fotosInstalacion[0] = servicio.FotoInstalacion1Url;
            _fotosInstalacion[1] = servicio.FotoInstalacion2Url;
            _fotosInstalacion[2] = servicio.FotoInstalacion3Url;
            _fotosInstalacion[3] = servicio.FotoInstalacion4Url;
            _fotosInstalacion[4] = servicio.FotoInstalacion5Url;
            _fotosInstalacion[5] = servicio.FotoInstalacion6Url;
            _fotosInstalacion[6] = servicio.FotoInstalacion7Url;
            _fotosInstalacion[7] = servicio.FotoInstalacion8Url;
            _fotosInstalacion[8] = servicio.FotoInstalacion9Url;
            _fotosInstalacion[9] = servicio.FotoInstalacion10Url;

            PhEntry.Text = servicio.ValorPh?.ToString();
            ConductividadEntry.Text = servicio.ValorConductividad?.ToString();
            ConcentracionInhibidorEntry.Text = servicio.ValorConcentracion?.ToString();
            TurbidezPicker.SelectedItem = servicio.ValorTurbidez;

            ComentariosEntry.Text = servicio.Comentarios;
            ComentariosInstaladorEntry.Text = servicio.ComentariosInstalador;

            if (!string.IsNullOrWhiteSpace(servicio.InhibidoresUtilizados))
            {
                var partes = servicio.InhibidoresUtilizados.Split(':');
                InhibidorPicker.SelectedItem = ObtenerValorPicker(InhibidorPicker, partes[0]);
                InhibidorCantidadEntry.Text = partes.Length > 1 ? partes[1] : "";
            }
            if (!string.IsNullOrWhiteSpace(servicio.LimpiadoresUtilizados))
            {
                var partes = servicio.LimpiadoresUtilizados.Split(':');
                LimpiadorPicker.SelectedItem = ObtenerValorPicker(LimpiadorPicker, partes[0]);
                LimpiadorCantidadEntry.Text = partes.Length > 1 ? partes[1] : "";
            }
            if (!string.IsNullOrWhiteSpace(servicio.BiocidasUtilizados))
            {
                var partes = servicio.BiocidasUtilizados.Split(':');
                BiocidaPicker.SelectedItem = ObtenerValorPicker(BiocidaPicker, partes[0]);
                BiocidaCantidadEntry.Text = partes.Length > 1 ? partes[1] : "";
            }
            if (!string.IsNullOrWhiteSpace(servicio.AnticongelantesUtilizados))
            {
                var partes = servicio.AnticongelantesUtilizados.Split(':');
                AnticongelantePicker.SelectedItem = ObtenerValorPicker(AnticongelantePicker, partes[0]);
                AnticongelanteCantidadEntry.Text = partes.Length > 1 ? partes[1] : "";
            }

            ActualizarVistaPreviaImagenes();
        }

        private void OnInhibidorPickerChanged(object sender, EventArgs e)
        {
            if (InhibidorPicker.SelectedItem?.ToString() == "Otro")
                InhibidorOtroEntry.IsVisible = true;
            else
                InhibidorOtroEntry.IsVisible = false;
        }

        private void OnLimpiadorPickerChanged(object sender, EventArgs e)
        {
            if (LimpiadorPicker.SelectedItem?.ToString() == "Otro")
                LimpiadorOtroEntry.IsVisible = true;
            else
                LimpiadorOtroEntry.IsVisible = false;
        }

        private void OnBiocidaPickerChanged(object sender, EventArgs e)
        {
            if (BiocidaPicker.SelectedItem?.ToString() == "Otro")
                BiocidaOtroEntry.IsVisible = true;
            else
                BiocidaOtroEntry.IsVisible = false;
        }

        private void OnAnticongelantePickerChanged(object sender, EventArgs e)
        {
            if (AnticongelantePicker.SelectedItem?.ToString() == "Otro")
                AnticongelanteOtroEntry.IsVisible = true;
            else
                AnticongelanteOtroEntry.IsVisible = false;
        }

        private string? ObtenerValorPicker(Picker picker, string? valor)
        {
            if (valor == null) return null;
            foreach (var item in picker.ItemsSource.Cast<string>())
            {
                if (item.Equals(valor, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
            return null;
        }

        private string? ObtenerNombreArchivo(string? rutaCompleta)
        {
            return string.IsNullOrEmpty(rutaCompleta) ? null : Path.GetFileName(rutaCompleta);
        }

        

        private void OnTipoServicioPickerChanged(object sender, EventArgs e)
        {
            TipoServicioOtroEntry.IsVisible = TipoServicioPicker.SelectedItem?.ToString() == "Otros";
            if (!TipoServicioOtroEntry.IsVisible)
                TipoServicioOtroEntry.Text = string.Empty;
        }

        private void OnTipoInstalacionPickerChanged(object sender, EventArgs e)
        {
            TipoInstalacionOtroEntry.IsVisible = TipoInstalacionPicker.SelectedItem?.ToString() == "Otros";
            if (!TipoInstalacionOtroEntry.IsVisible)
                TipoInstalacionOtroEntry.Text = string.Empty;
        }

        private void OnFuenteCalorPickerChanged(object sender, EventArgs e)
        {
            FuenteCalorOtroEntry.IsVisible = FuenteCalorPicker.SelectedItem?.ToString() == "Otros";
            if (!FuenteCalorOtroEntry.IsVisible)
                FuenteCalorOtroEntry.Text = string.Empty;
        }

        private void OnTurbidezPickerChanged(object sender, EventArgs e)
        {
            // Lógica opcional si necesitas reaccionar al cambio de turbidez
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_dbInitialized)
            {
                await _db.InitAsync();
                _dbInitialized = true;
            }
        }

        private async void OnNombreEntryUnfocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry && !string.IsNullOrWhiteSpace(entry.Text))
            {
                var clientes = await _db.ObtenerClientesAsync();
                var cliente = clientes.FirstOrDefault(c => c.NombreCliente.Equals(entry.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (cliente != null)
                {
                    DireccionEntry.Text = cliente.Direccion;
                    EmailEntry.Text = cliente.Email;
                    TelefonoEntry.Text = cliente.Telefono;
                    _clienteSeleccionado = cliente;
                }
            }
        }

        private void OnDireccionEntryUnfocused(object sender, FocusEventArgs e) { }
        private void OnTelefonoEntryUnfocused(object sender, FocusEventArgs e) { }

        private void ActualizarVistaPreviaImagenes()
        {
            ImagenesAdjuntas.Clear();
            if (!string.IsNullOrEmpty(_phLocalPath)) ImagenesAdjuntas.Add(_phLocalPath);
            if (!string.IsNullOrEmpty(_condLocalPath)) ImagenesAdjuntas.Add(_condLocalPath);
            if (!string.IsNullOrEmpty(_concLocalPath)) ImagenesAdjuntas.Add(_concLocalPath);
            if (!string.IsNullOrEmpty(_turbLocalPath)) ImagenesAdjuntas.Add(_turbLocalPath);
            foreach (var foto in _fotosInstalacion)
                if (!string.IsNullOrEmpty(foto)) ImagenesAdjuntas.Add(foto);
        }

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

        private async Task<string?> GuardarFotoLocalAsync(FileResult file, string slug)
        {
            try
            {
                var localFolder = FileSystem.AppDataDirectory;
                var localPath = Path.Combine(localFolder, $"{slug}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}");

                await using (var localStream = File.Create(localPath))
                {
                    await using var fileStream = await file.OpenReadAsync();
                    await fileStream.CopyToAsync(localStream);
                }

                return localPath;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo guardar la foto: {ex.Message}", "OK");
                return null;
            }
        }

        private async void OnAdjuntarPhFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _phLocalPath = await GuardarFotoLocalAsync(f, "ph");
            ActualizarVistaPreviaImagenes();
        }
        private async void OnAdjuntarConductividadFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _condLocalPath = await GuardarFotoLocalAsync(f, "conductividad");
            ActualizarVistaPreviaImagenes();
        }
        private async void OnAdjuntarConcentracionFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _concLocalPath = await GuardarFotoLocalAsync(f, "concentracion");
            ActualizarVistaPreviaImagenes();
        }
        private async void OnAdjuntarTurbidezFotoClicked(object sender, EventArgs e)
        {
            if (await SeleccionarFotoAsync() is FileResult f)
                _turbLocalPath = await GuardarFotoLocalAsync(f, "turbidez");
            ActualizarVistaPreviaImagenes();
        }

        private async void OnAdjuntarFotosInstalacionClicked(object sender, EventArgs e)
        {
            try
            {
                var maxFotos = 10 - _fotosInstalacion.Count(f => !string.IsNullOrEmpty(f));
                if (maxFotos <= 0)
                {
                    await DisplayAlert("Límite alcanzado", "Ya has adjuntado 10 fotos de instalación.", "OK");
                    return;
                }

                var accion = await DisplayActionSheet("Seleccionar foto", "Cancelar", null, "Cámara", "Galería", "Archivos");
                if (accion == "Cancelar") return;

                if (accion == "Cámara" && MediaPicker.Default.IsCaptureSupported)
                {
                    var file = await MediaPicker.Default.CapturePhotoAsync();
                    if (file != null)
                    {
                        for (int i = 0; i < _fotosInstalacion.Length; i++)
                        {
                            if (string.IsNullOrEmpty(_fotosInstalacion[i]))
                            {
                                _fotosInstalacion[i] = await GuardarFotoLocalAsync(file, $"instalacion{i + 1}");
                                break;
                            }
                        }
                    }
                }
                else if (accion == "Galería")
                {
                    var file = await MediaPicker.Default.PickPhotoAsync();
                    if (file != null)
                    {
                        for (int i = 0; i < _fotosInstalacion.Length; i++)
                        {
                            if (string.IsNullOrEmpty(_fotosInstalacion[i]))
                            {
                                _fotosInstalacion[i] = await GuardarFotoLocalAsync(file, $"instalacion{i + 1}");
                                break;
                            }
                        }
                    }
                }
                else if (accion == "Archivos")
                {
                    var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
                    {
                        PickerTitle = $"Selecciona hasta {maxFotos} imágenes",
                        FileTypes = FilePickerFileType.Images
                    });

                    if (files != null)
                    {
                        int idx = 0;
                        for (int i = 0; i < _fotosInstalacion.Length && idx < files.Count() && idx < maxFotos; i++)
                        {
                            if (string.IsNullOrEmpty(_fotosInstalacion[i]))
                            {
                                var file = files.ElementAt(idx);
                                _fotosInstalacion[i] = await GuardarFotoLocalAsync(file, $"instalacion{i + 1}");
                                idx++;
                            }
                        }
                    }
                }
                ActualizarVistaPreviaImagenes();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudieron adjuntar las fotos: {ex.Message}", "OK");
            }
        }

        // Métodos para eliminar fotos analíticas con confirmación
        private async void OnEliminarPhFotoClicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert("Eliminar foto", "¿Seguro que quieres eliminar esta foto?", "Sí", "No");
            if (!confirmar) return;
            EliminarFotoLocal(_phLocalPath);
            _phLocalPath = null;
            ActualizarVistaPreviaImagenes();
        }
        private async void OnEliminarConductividadFotoClicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert("Eliminar foto", "¿Seguro que quieres eliminar esta foto?", "Sí", "No");
            if (!confirmar) return;
            EliminarFotoLocal(_condLocalPath);
            _condLocalPath = null;
            ActualizarVistaPreviaImagenes();
        }
        private async void OnEliminarConcentracionFotoClicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert("Eliminar foto", "¿Seguro que quieres eliminar esta foto?", "Sí", "No");
            if (!confirmar) return;
            EliminarFotoLocal(_concLocalPath);
            _concLocalPath = null;
            ActualizarVistaPreviaImagenes();
        }
        private async void OnEliminarTurbidezFotoClicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert("Eliminar foto", "¿Seguro que quieres eliminar esta foto?", "Sí", "No");
            if (!confirmar) return;
            EliminarFotoLocal(_turbLocalPath);
            _turbLocalPath = null;
            ActualizarVistaPreviaImagenes();
        }

        // Método para eliminar fotos de instalación desde la vista previa con confirmación
        private async void OnEliminarFotoInstalacionClicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert("Eliminar foto", "¿Seguro que quieres eliminar esta foto?", "Sí", "No");
            if (!confirmar) return;
            if (sender is Button btn && btn.BindingContext is string path)
            {
                int idx = Array.IndexOf(_fotosInstalacion, path);
                if (idx >= 0)
                {
                    EliminarFotoLocal(_fotosInstalacion[idx]);
                    _fotosInstalacion[idx] = null;
                    ActualizarVistaPreviaImagenes();
                }
            }
        }

        private void EliminarFotoLocal(string? localPath)
        {
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            {
                try { File.Delete(localPath); } catch { }
            }
        }

        private string? SerializarProducto(Picker picker, Entry cantidadEntry, Entry? otroEntry = null)
        {
            string? nombre = null;
            if (picker.SelectedItem is string seleccionado)
            {
                if (seleccionado.ToLower() == "otros" && otroEntry != null && !string.IsNullOrWhiteSpace(otroEntry.Text))
                    nombre = otroEntry.Text.Trim();
                else if (seleccionado.ToLower() != "otros")
                    nombre = seleccionado;
            }
            if (!string.IsNullOrWhiteSpace(nombre) && !string.IsNullOrWhiteSpace(cantidadEntry.Text) && int.TryParse(cantidadEntry.Text, out var cantidad) && cantidad > 0)
                return $"{nombre}:{cantidad}";
            return null;
        }

        private string SerializarCategoria(params string?[] productos)
        {
            return string.Join(";", productos.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        private DateTime FechaAhora => DateTime.Now;

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            if (IsGuardando) return;
            IsGuardando = true;

            try
            {
                if (NombreEntry == null)
                {
                    await DisplayAlert("Error", "El control NombreEntry no está inicializado.", "OK");
                    return;
                }
                if (string.IsNullOrWhiteSpace(NombreEntry.Text))
                {
                    await DisplayAlert("Error", "El nombre del cliente es obligatorio", "OK");
                    return;
                }

                Exception? error = null;
                bool edicion = _servicioEditando != null;

                await Task.Run(async () =>
                {
                    try
                    {
                        Cliente cliente;
                        if (_clienteSeleccionado != null)
                        {
                            _clienteSeleccionado.NombreCliente = NombreEntry.Text!.Trim();
                            _clienteSeleccionado.Direccion = DireccionEntry.Text?.Trim();
                            _clienteSeleccionado.Email = EmailEntry.Text?.Trim();
                            _clienteSeleccionado.Telefono = TelefonoEntry.Text?.Trim();
                            await _db.ActualizarClienteAsync(_clienteSeleccionado);
                            cliente = _clienteSeleccionado;
                        }
                        else
                        {
                            cliente = new Cliente
                            {
                                NombreCliente = NombreEntry.Text!.Trim(),
                                Direccion = DireccionEntry.Text?.Trim(),
                                Email = EmailEntry.Text?.Trim(),
                                Telefono = TelefonoEntry.Text?.Trim()
                            };
                            cliente.Id = await _db.GuardarClienteAsync(cliente);
                            if (cliente.Id == 0)
                                throw new Exception("No se pudo guardar el cliente correctamente.");
                        }

                        int? antiguedadInstalacion = int.TryParse(AntiguedadInstalacionEntry?.Text, out var ai) ? ai : null;
                        int? antiguedadAparato = int.TryParse(AntiguedadAparatoProduccionEntry?.Text, out var aa) ? aa : null;
                        string modelo = ModeloEntry?.Text?.Trim() ?? string.Empty;
                        string marca = MarcaEntry?.Text?.Trim() ?? string.Empty;
                        int? ultimaRevision = int.TryParse(UltimaRevisionEntry?.Text, out var ur) ? ur : null;
                        string proximaVisita = ProximaVisitaPicker.SelectedItem?.ToString() ?? string.Empty;

                        var inhibidor = SerializarProducto(InhibidorPicker, InhibidorCantidadEntry, InhibidorOtroEntry);
                        var limpiador = SerializarProducto(LimpiadorPicker, LimpiadorCantidadEntry, LimpiadorOtroEntry);
                        var biocida = SerializarProducto(BiocidaPicker, BiocidaCantidadEntry, BiocidaOtroEntry);
                        var anticongelante = SerializarProducto(AnticongelantePicker, AnticongelanteCantidadEntry, AnticongelanteOtroEntry);

                        var ahora = FechaAhora;

                        if (edicion)
                        {
                            var servicio = _servicioEditando!;
                            servicio.ClienteId = cliente.Id;
                            servicio.FechaModificacionFecha = ahora;

                            servicio.TipoServicio = TipoServicioPicker?.SelectedItem?.ToString() == "Otros"
                                ? TipoServicioOtroEntry.Text
                                : TipoServicioPicker?.SelectedItem?.ToString();
                            servicio.FechaModificacionTipoServicio = ahora;

                            servicio.TipoInstalacion = TipoInstalacionPicker?.SelectedItem?.ToString() == "Otros"
                                ? TipoInstalacionOtroEntry.Text
                                : TipoInstalacionPicker?.SelectedItem?.ToString();
                            servicio.FechaModificacionTipoInstalacion = ahora;

                            servicio.FuenteCalor = FuenteCalorPicker?.SelectedItem?.ToString() == "Otros"
                                ? FuenteCalorOtroEntry.Text
                                : FuenteCalorPicker?.SelectedItem?.ToString();
                            servicio.FechaModificacionFuenteCalor = ahora;

                            servicio.ValorPh = double.TryParse(PhEntry?.Text, out var ph) ? ph : null;
                            servicio.FechaModificacionValorPh = ahora;

                            servicio.ValorConductividad = double.TryParse(ConductividadEntry?.Text, out var c) ? c : null;
                            servicio.FechaModificacionValorConductividad = ahora;

                            servicio.ValorConcentracion = double.TryParse(ConcentracionInhibidorEntry?.Text, out var ci) ? ci : null;
                            servicio.FechaModificacionValorConcentracion = ahora;

                            servicio.ValorTurbidez = TurbidezPicker.SelectedItem?.ToString();
                            servicio.FechaModificacionValorTurbidez = ahora;

                            servicio.ProximaVisita = proximaVisita;
                            servicio.FechaModificacionProximaVisita = ahora;

                            servicio.FotoPhUrl = ObtenerNombreArchivo(_phLocalPath);
                            servicio.FechaModificacionFotoPhUrl = ahora;

                            servicio.FotoConductividadUrl = ObtenerNombreArchivo(_condLocalPath);
                            servicio.FechaModificacionFotoConductividadUrl = ahora;

                            servicio.FotoConcentracionUrl = ObtenerNombreArchivo(_concLocalPath);
                            servicio.FechaModificacionFotoConcentracionUrl = ahora;

                            servicio.FotoTurbidezUrl = ObtenerNombreArchivo(_turbLocalPath);
                            servicio.FechaModificacionFotoTurbidezUrl = ahora;

                            servicio.Comentarios = ComentariosEntry?.Text;
                            servicio.ComentariosInstalador = ComentariosInstaladorEntry?.Text;

                            servicio.FotoInstalacion1Url = ObtenerNombreArchivo(_fotosInstalacion[0]);
                            servicio.FechaModificacionFotoInstalacion1Url = ahora;
                            servicio.FotoInstalacion2Url = ObtenerNombreArchivo(_fotosInstalacion[1]);
                            servicio.FechaModificacionFotoInstalacion2Url = ahora;
                            servicio.FotoInstalacion3Url = ObtenerNombreArchivo(_fotosInstalacion[2]);
                            servicio.FechaModificacionFotoInstalacion3Url = ahora;
                            servicio.FotoInstalacion4Url = ObtenerNombreArchivo(_fotosInstalacion[3]);
                            servicio.FechaModificacionFotoInstalacion4Url = ahora;
                            servicio.FotoInstalacion5Url = ObtenerNombreArchivo(_fotosInstalacion[4]);
                            servicio.FechaModificacionFotoInstalacion5Url = ahora;
                            servicio.FotoInstalacion6Url = ObtenerNombreArchivo(_fotosInstalacion[5]);
                            servicio.FechaModificacionFotoInstalacion6Url = ahora;
                            servicio.FotoInstalacion7Url = ObtenerNombreArchivo(_fotosInstalacion[6]);
                            servicio.FechaModificacionFotoInstalacion7Url = ahora;
                            servicio.FotoInstalacion8Url = ObtenerNombreArchivo(_fotosInstalacion[7]);
                            servicio.FechaModificacionFotoInstalacion8Url = ahora;
                            servicio.FotoInstalacion9Url = ObtenerNombreArchivo(_fotosInstalacion[8]);
                            servicio.FechaModificacionFotoInstalacion9Url = ahora;
                            servicio.FotoInstalacion10Url = ObtenerNombreArchivo(_fotosInstalacion[9]);
                            servicio.FechaModificacionFotoInstalacion10Url = ahora;

                            servicio.InhibidoresUtilizados = SerializarCategoria(inhibidor);
                            servicio.FechaModificacionInhibidoresUtilizados = ahora;
                            servicio.LimpiadoresUtilizados = SerializarCategoria(limpiador);
                            servicio.FechaModificacionLimpiadoresUtilizados = ahora;
                            servicio.BiocidasUtilizados = SerializarCategoria(biocida);
                            servicio.FechaModificacionBiocidasUtilizados = ahora;
                            servicio.AnticongelantesUtilizados = SerializarCategoria(anticongelante);
                            servicio.FechaModificacionAnticongelantesUtilizados = ahora;

                            servicio.EquipamientoUtilizado = EquipamientoPicker?.SelectedItem?.ToString();
                            servicio.FechaModificacionEquipamientoUtilizado = ahora;

                            servicio.AntiguedadInstalacion = antiguedadInstalacion;
                            servicio.FechaModificacionAntiguedadInstalacion = ahora;
                            servicio.AntiguedadAparatoProduccion = antiguedadAparato;
                            servicio.FechaModificacionAntiguedadAparatoProduccion = ahora;
                            servicio.Modelo = modelo;
                            servicio.FechaModificacionModelo = ahora;
                            servicio.Marca = marca;
                            servicio.FechaModificacionMarca = ahora;
                            servicio.UltimaRevision = ultimaRevision;
                            servicio.FechaModificacionUltimaRevision = ahora;

                            await _db.ActualizarServicioAsync(servicio);
                        }
                        else
                        {
                            var servicio = new Servicio
                            {
                                ClienteId = cliente.Id,
                                Fecha = ahora,
                                FechaModificacionFecha = ahora,
                                TipoServicio = TipoServicioPicker?.SelectedItem?.ToString() == "Otros"
                                    ? TipoServicioOtroEntry.Text
                                    : TipoServicioPicker?.SelectedItem?.ToString(),
                                FechaModificacionTipoServicio = ahora,
                                TipoInstalacion = TipoInstalacionPicker?.SelectedItem?.ToString() == "Otros"
                                    ? TipoInstalacionOtroEntry.Text
                                    : TipoInstalacionPicker?.SelectedItem?.ToString(),
                                FechaModificacionTipoInstalacion = ahora,
                                FuenteCalor = FuenteCalorPicker?.SelectedItem?.ToString() == "Otros"
                                    ? FuenteCalorOtroEntry.Text
                                    : FuenteCalorPicker?.SelectedItem?.ToString(),
                                FechaModificacionFuenteCalor = ahora,
                                ValorPh = double.TryParse(PhEntry?.Text, out var ph) ? ph : null,
                                FechaModificacionValorPh = ahora,
                                ValorConductividad = double.TryParse(ConductividadEntry?.Text, out var c) ? c : null,
                                FechaModificacionValorConductividad = ahora,
                                ValorConcentracion = double.TryParse(ConcentracionInhibidorEntry?.Text, out var ci) ? ci : null,
                                FechaModificacionValorConcentracion = ahora,
                                ValorTurbidez = TurbidezPicker.SelectedItem?.ToString(),
                                FechaModificacionValorTurbidez = ahora,
                                ProximaVisita = proximaVisita,
                                FechaModificacionProximaVisita = ahora,
                                FotoPhUrl = ObtenerNombreArchivo(_phLocalPath),
                                FechaModificacionFotoPhUrl = ahora,
                                FotoConductividadUrl = ObtenerNombreArchivo(_condLocalPath),
                                FechaModificacionFotoConductividadUrl = ahora,
                                FotoConcentracionUrl = ObtenerNombreArchivo(_concLocalPath),
                                FechaModificacionFotoConcentracionUrl = ahora,
                                FotoTurbidezUrl = ObtenerNombreArchivo(_turbLocalPath),
                                FechaModificacionFotoTurbidezUrl = ahora,
                                Comentarios = ComentariosEntry?.Text,
                                ComentariosInstalador = ComentariosInstaladorEntry?.Text,
                                FotoInstalacion1Url = ObtenerNombreArchivo(_fotosInstalacion[0]),
                                FechaModificacionFotoInstalacion1Url = ahora,
                                FotoInstalacion2Url = ObtenerNombreArchivo(_fotosInstalacion[1]),
                                FechaModificacionFotoInstalacion2Url = ahora,
                                FotoInstalacion3Url = ObtenerNombreArchivo(_fotosInstalacion[2]),
                                FechaModificacionFotoInstalacion3Url = ahora,
                                FotoInstalacion4Url = ObtenerNombreArchivo(_fotosInstalacion[3]),
                                FechaModificacionFotoInstalacion4Url = ahora,
                                FotoInstalacion5Url = ObtenerNombreArchivo(_fotosInstalacion[4]),
                                FechaModificacionFotoInstalacion5Url = ahora,
                                FotoInstalacion6Url = ObtenerNombreArchivo(_fotosInstalacion[5]),
                                FechaModificacionFotoInstalacion6Url = ahora,
                                FotoInstalacion7Url = ObtenerNombreArchivo(_fotosInstalacion[6]),
                                FechaModificacionFotoInstalacion7Url = ahora,
                                FotoInstalacion8Url = ObtenerNombreArchivo(_fotosInstalacion[7]),
                                FechaModificacionFotoInstalacion8Url = ahora,
                                FotoInstalacion9Url = ObtenerNombreArchivo(_fotosInstalacion[8]),
                                FechaModificacionFotoInstalacion9Url = ahora,
                                FotoInstalacion10Url = ObtenerNombreArchivo(_fotosInstalacion[9]),
                                FechaModificacionFotoInstalacion10Url = ahora,
                                InhibidoresUtilizados = SerializarCategoria(inhibidor),
                                FechaModificacionInhibidoresUtilizados = ahora,
                                LimpiadoresUtilizados = SerializarCategoria(limpiador),
                                FechaModificacionLimpiadoresUtilizados = ahora,
                                BiocidasUtilizados = SerializarCategoria(biocida),
                                FechaModificacionBiocidasUtilizados = ahora,
                                AnticongelantesUtilizados = SerializarCategoria(anticongelante),
                                FechaModificacionAnticongelantesUtilizados = ahora,
                                EquipamientoUtilizado = EquipamientoPicker?.SelectedItem?.ToString(),
                                FechaModificacionEquipamientoUtilizado = ahora,
                                AntiguedadInstalacion = antiguedadInstalacion,
                                FechaModificacionAntiguedadInstalacion = ahora,
                                AntiguedadAparatoProduccion = antiguedadAparato,
                                FechaModificacionAntiguedadAparatoProduccion = ahora,
                                Modelo = modelo,
                                FechaModificacionModelo = ahora,
                                Marca = marca,
                                FechaModificacionMarca = ahora,
                                UltimaRevision = ultimaRevision,
                                FechaModificacionUltimaRevision = ahora
                            };

                            var idServicio = await _db.GuardarServicioAsync(servicio);
                            if (idServicio > 0)
                                servicio.Id = idServicio;
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                });

                if (error != null)
                {
                    await DisplayAlert("Error", $"No se pudo guardar: {error.Message}", "OK");
                }
                else
                {
                    LimpiarCampos();
                    await DisplayAlert("Éxito", "Datos guardados correctamente en local.", "OK");
                    if (edicion)
                        await Navigation.PopAsync();
                }
            }
            finally
            {
                IsGuardando = false;
            }
        }

        private async void OnResetClicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert(
                "Confirmar reseteo",
                "¿Estás seguro que quieres limpiar todos los campos del formulario?",
                "Sí", "No");

            if (confirmar)
            {
                LimpiarCampos();
            }
        }

        private void LimpiarCampos()
        {
            NombreEntry.Text = string.Empty;
            DireccionEntry.Text = string.Empty;
            EmailEntry.Text = string.Empty;
            TelefonoEntry.Text = string.Empty;
            PhEntry.Text = string.Empty;
            ConductividadEntry.Text = string.Empty;
            ConcentracionInhibidorEntry.Text = string.Empty;
            ComentariosEntry.Text = string.Empty;
            ComentariosInstaladorEntry.Text = string.Empty;

            TurbidezPicker.SelectedIndex = -1;
            ProximaVisitaPicker.SelectedIndex = -1;

            AntiguedadInstalacionEntry.Text = string.Empty;
            AntiguedadAparatoProduccionEntry.Text = string.Empty;
            ModeloEntry.Text = string.Empty;
            MarcaEntry.Text = string.Empty;
            UltimaRevisionEntry.Text = string.Empty;

            TipoServicioPicker.SelectedIndex = -1;
            TipoInstalacionPicker.SelectedIndex = -1;
            FuenteCalorPicker.SelectedIndex = -1;
            InhibidorPicker.SelectedIndex = -1;
            LimpiadorPicker.SelectedIndex = -1;
            BiocidaPicker.SelectedIndex = -1;
            AnticongelantePicker.SelectedIndex = -1;
            EquipamientoPicker.SelectedIndex = -1;

            InhibidorCantidadEntry.Text = string.Empty;
            LimpiadorCantidadEntry.Text = string.Empty;
            BiocidaCantidadEntry.Text = string.Empty;
            AnticongelanteCantidadEntry.Text = string.Empty;

            InhibidorOtroEntry.Text = string.Empty;
            InhibidorOtroEntry.IsVisible = false;
            LimpiadorOtroEntry.Text = string.Empty;
            LimpiadorOtroEntry.IsVisible = false;
            BiocidaOtroEntry.Text = string.Empty;
            BiocidaOtroEntry.IsVisible = false;
            AnticongelanteOtroEntry.Text = string.Empty;
            AnticongelanteOtroEntry.IsVisible = false;

            _phLocalPath = null;
            _condLocalPath = null;
            _concLocalPath = null;
            _turbLocalPath = null;

            for (int i = 0; i < _fotosInstalacion.Length; i++)
                _fotosInstalacion[i] = null;

            _clienteSeleccionado = null;
            ImagenesAdjuntas.Clear();
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
        protected new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}