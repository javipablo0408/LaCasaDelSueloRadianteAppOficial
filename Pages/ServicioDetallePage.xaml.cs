using LaCasaDelSueloRadianteApp.Services;
using LaCasaDelSueloRadianteApp.Models;
using Microsoft.Maui.Controls;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Drawing;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LaCasaDelSueloRadianteApp;

public partial class ServicioDetallePage : ContentPage
{
    private readonly IImageService _imgSvc;
    private string? _lastNombreArchivo;
    private readonly Servicio _servicio;
    private readonly Cliente _cliente;
    private readonly Instalador _instalador;

    public ServicioDetallePage(Servicio servicio, Cliente cliente, Instalador instalador, IImageService imgSvc)
    {
        InitializeComponent();

        BindingContext = new
        {
            Cliente = cliente,
            Servicio = servicio
        };

        _imgSvc = imgSvc;
        _servicio = servicio;
        _cliente = cliente;
        _instalador = instalador;
    }

    private async void OnEditarServicioClicked(object sender, EventArgs e)
    {
        var db = App.Services.GetService<DatabaseService>();
        var oneDrive = App.Services.GetService<OneDriveService>();

        if (db == null || oneDrive == null)
        {
            await DisplayAlert("Error", "No se pudo obtener los servicios necesarios.", "OK");
            return;
        }

        await Navigation.PushAsync(new AgregarPage(db, _servicio, _cliente));
    }

    private async void OnImgTapped(object? sender, string nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo))
            return;

        _lastNombreArchivo = nombreArchivo;
        try
        {
            await Navigation.PushAsync(new FullScreenImagePage(nombreArchivo));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo mostrar la imagen: {ex.Message}", "OK");
        }
    }

    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastNombreArchivo))
        {
            await DisplayAlert("Info", "Toca una imagen primero", "OK");
            return;
        }

        DownloadBar.Progress = 0;
        DownloadBar.IsVisible = true;

        try
        {
            var prog = new Progress<double>(p => DownloadBar.Progress = p);
            var path = await _imgSvc.DownloadAndSaveAsync(_lastNombreArchivo, prog);

            DownloadBar.IsVisible = false;

            if (string.IsNullOrEmpty(path))
            {
                await DisplayAlert("Error", "No se pudo guardar la imagen", "OK");
            }
            else
            {
                await DisplayAlert("Éxito", "Imagen guardada", "Ver");
                await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(path) });
            }
        }
        catch (Exception ex)
        {
            DownloadBar.IsVisible = false;
            await DisplayAlert("Error", $"Ocurrió un error durante la descarga: {ex.Message}", "OK");
        }
    }

    private async void OnGenerarPdfClicked(object sender, EventArgs e)
    {
        try
        {
            var pdfBytes = GenerarInformePdf(_cliente, _servicio, _instalador);

            var fileName = $"Informe_{_servicio.Fecha:yyyyMMdd_HHmm}.pdf";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            File.WriteAllBytes(filePath, pdfBytes);

            await Launcher.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo generar el PDF: {ex.Message}", "OK");
        }
    }

    private byte[] GenerarInformePdf(Cliente cliente, Servicio servicio, Instalador instalador)
    {
        using var document = new PdfDocument();
        var page = document.Pages.Add();

        float y = 0;
        float leftMargin = 30;
        float rightMargin = 30;
        float labelWidth = 130;
        float bottomMargin = 30;
        float pageHeight = page.GetClientSize().Height;
        float pageWidth = page.GetClientSize().Width;

        // Espaciados
        float espaciadoSeccion = 18;
        float espaciadoLinea = 10;
        float espaciadoBloque = 18;
        float espaciadoFoto = 30;

        // Función local para salto de página
        void NuevaPaginaSiNecesario(float alturaNecesaria)
        {
            if (y + alturaNecesaria > pageHeight - bottomMargin)
            {
                page = document.Pages.Add();
                y = 0;
            }
        }

        // Fuentes
        var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
        var sectionFont = new PdfStandardFont(PdfFontFamily.Helvetica, 16, PdfFontStyle.Bold);
        var labelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
        var valueFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12);

        // Título
        NuevaPaginaSiNecesario(30);
        page.Graphics.DrawString("Informe de Servicio de Limpieza", titleFont, PdfBrushes.DarkBlue, new Syncfusion.Drawing.PointF(leftMargin, y));
        y += 30 + espaciadoSeccion;

        // Fecha
        var subFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12);
        if (servicio.Fecha != default)
        {
            NuevaPaginaSiNecesario(20);
            page.Graphics.DrawString($"Fecha: {servicio.Fecha:dd/MM/yyyy}", subFont, PdfBrushes.Gray, new Syncfusion.Drawing.PointF(leftMargin, y));
            y += 20 + espaciadoBloque;
        }

        // Línea separadora
        NuevaPaginaSiNecesario(10);
        page.Graphics.DrawLine(new PdfPen(PdfBrushes.DarkBlue, 1),
            new Syncfusion.Drawing.PointF(leftMargin, y),
            new Syncfusion.Drawing.PointF(pageWidth - rightMargin, y));
        y += 10 + espaciadoBloque;

        // Detalles del Servicio
        bool hayDetallesServicio = !string.IsNullOrWhiteSpace(instalador.Empresa)
            || !string.IsNullOrWhiteSpace(servicio.TipoServicio)
            || !string.IsNullOrWhiteSpace(instalador.Nombre);

        if (hayDetallesServicio)
        {
            NuevaPaginaSiNecesario(22);
            page.Graphics.DrawString("Detalles del Servicio", sectionFont, PdfBrushes.DarkBlue, new Syncfusion.Drawing.PointF(leftMargin, y));
            y += 22 + espaciadoLinea;

            if (!string.IsNullOrWhiteSpace(instalador.Empresa))
            {
                NuevaPaginaSiNecesario(18);
                page.Graphics.DrawString("Empresa:", labelFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
                page.Graphics.DrawString(instalador.Empresa, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin + labelWidth, y));
                y += 18 + espaciadoLinea;
            }
            if (!string.IsNullOrWhiteSpace(servicio.TipoServicio))
            {
                page.Graphics.DrawString("Tipo de Servicio:", labelFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
                page.Graphics.DrawString(servicio.TipoServicio, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin + labelWidth, y));
                y += 18 + espaciadoLinea;
            }
            if (!string.IsNullOrWhiteSpace(instalador.Nombre))
            {
                page.Graphics.DrawString("Instalador:", labelFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
                page.Graphics.DrawString(instalador.Nombre, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin + labelWidth, y));
                y += 18 + espaciadoBloque;
            }
        }

        // Datos del Cliente
        bool hayDatosCliente = !string.IsNullOrWhiteSpace(cliente.NombreCliente)
            || !string.IsNullOrWhiteSpace(cliente.Direccion)
            || !string.IsNullOrWhiteSpace(cliente.Telefono);

        if (hayDatosCliente)
        {
            NuevaPaginaSiNecesario(22);
            page.Graphics.DrawString("Datos del Cliente", sectionFont, PdfBrushes.DarkBlue, new Syncfusion.Drawing.PointF(leftMargin, y));
            y += 22 + espaciadoLinea;

            if (!string.IsNullOrWhiteSpace(cliente.NombreCliente))
            {
                page.Graphics.DrawString("Nombre:", labelFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
                page.Graphics.DrawString(cliente.NombreCliente, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin + labelWidth, y));
                y += 18 + espaciadoLinea;
            }
            if (!string.IsNullOrWhiteSpace(cliente.Direccion))
            {
                page.Graphics.DrawString("Dirección:", labelFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
                page.Graphics.DrawString(cliente.Direccion, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin + labelWidth, y));
                y += 18 + espaciadoLinea;
            }
            if (!string.IsNullOrWhiteSpace(cliente.Telefono))
            {
                page.Graphics.DrawString("Teléfono:", labelFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
                page.Graphics.DrawString(cliente.Telefono, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin + labelWidth, y));
                y += 18 + espaciadoBloque;
            }
        }

        // Equipamiento Utilizado
        if (!string.IsNullOrWhiteSpace(servicio.EquipamientoUtilizado))
        {
            NuevaPaginaSiNecesario(20 + 18);
            page.Graphics.DrawString("Equipamiento Utilizado", sectionFont, PdfBrushes.DarkBlue, new Syncfusion.Drawing.PointF(leftMargin, y));
            y += 20 + espaciadoLinea;
            page.Graphics.DrawString(servicio.EquipamientoUtilizado, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
            y += 18 + espaciadoBloque;
        }

        // Productos y Procedimientos (con altura dinámica)
        bool hayProductos = !string.IsNullOrWhiteSpace(servicio.LimpiadoresUtilizados)
            || !string.IsNullOrWhiteSpace(servicio.InhibidoresUtilizados)
            || !string.IsNullOrWhiteSpace(servicio.BiocidasUtilizados);

        if (hayProductos)
        {
            NuevaPaginaSiNecesario(20);
            page.Graphics.DrawString("Productos y Procedimientos", sectionFont, PdfBrushes.DarkBlue, new Syncfusion.Drawing.PointF(leftMargin, y));
            y += 20 + espaciadoLinea;
            string productos = "";
            if (!string.IsNullOrWhiteSpace(servicio.LimpiadoresUtilizados))
                productos += $"Hemos utilizado el limpiador {servicio.LimpiadoresUtilizados} que es el encargado de movilizar y eliminar los depósitos de cal y lodo.\n";
            if (!string.IsNullOrWhiteSpace(servicio.InhibidoresUtilizados))
                productos += $"Se ha añadido inhibidor {servicio.InhibidoresUtilizados}. Este producto está desarrollado para evitar la corrosión en los metales propios de la instalación así como las incrustaciones calcáreas.\n";
            if (!string.IsNullOrWhiteSpace(servicio.BiocidasUtilizados))
                productos += $"Además, hemos añadido biocida {servicio.BiocidasUtilizados}, necesario en instalaciones de baja temperatura donde se desarrollan bacterias que pueden generar corrosión bacteriana.";

            float anchoTexto = pageWidth - leftMargin - rightMargin;
            var size = valueFont.MeasureString(productos, anchoTexto);
            float alturaProductos = size.Height + 8;

            NuevaPaginaSiNecesario(alturaProductos);
            page.Graphics.DrawString(productos, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.RectangleF(leftMargin, y, anchoTexto, alturaProductos));
            y += alturaProductos + espaciadoBloque;
        }

        // Resultados del Análisis del Agua
        bool hayAnalisis = servicio.ValorPh != null || servicio.ValorConductividad != null ||
                           servicio.ValorConcentracion != null || servicio.ValorTurbidez != null;

        if (hayAnalisis)
        {
            NuevaPaginaSiNecesario(20 + 16 * 4 + espaciadoBloque);
            page.Graphics.DrawString("Resultados del Análisis del Agua", sectionFont, PdfBrushes.DarkBlue, new Syncfusion.Drawing.PointF(leftMargin, y));
            y += 20 + espaciadoLinea;

            if (servicio.ValorPh != null)
            {
                page.Graphics.DrawString($"Nivel de pH: {servicio.ValorPh:F2} (Normal: 7 - 8.5)", valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
                y += 16 + espaciadoLinea;
            }
            if (servicio.ValorConductividad != null)
            {
                page.Graphics.DrawString($"Conductividad: {servicio.ValorConductividad:F2} (Normal: 800 - 1300 picoSiemens)", valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
                y += 16 + espaciadoLinea;
            }
            if (servicio.ValorConcentracion != null)
            {
                page.Graphics.DrawString($"Concentración de Inhibidor: {servicio.ValorConcentracion:F2}% (Normal: 0.5% - 2%)", valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
                y += 16 + espaciadoLinea;
            }
            if (servicio.ValorTurbidez != null)
            {
                page.Graphics.DrawString($"Turbidez del Agua: {servicio.ValorTurbidez:F2}", valueFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(leftMargin, y));
                y += 16 + espaciadoBloque;
            }
        }

        // Recomendaciones (altura dinámica y próxima visita en meses)
        NuevaPaginaSiNecesario(20);
        page.Graphics.DrawString("Recomendaciones", sectionFont, PdfBrushes.DarkBlue, new Syncfusion.Drawing.PointF(leftMargin, y));
        y += 20 + espaciadoLinea;

        string textoRecom;
        if (!string.IsNullOrWhiteSpace(servicio.ProximaVisita))
        {
            textoRecom = $"Es recomendable realizar una visita de control en {servicio.ProximaVisita} meses, donde revisaremos estos valores y recomendaremos acciones preventivas si son necesarias.";
        }
        else
        {
            textoRecom = "Es recomendable realizar una visita de control donde revisaremos estos valores y recomendaremos acciones preventivas si son necesarias.";
        }

        float anchoRecom = pageWidth - leftMargin - rightMargin;
        var sizeRecom = valueFont.MeasureString(textoRecom, anchoRecom);
        float alturaRecom = sizeRecom.Height + 8;
        NuevaPaginaSiNecesario(alturaRecom);
        page.Graphics.DrawString(textoRecom, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.RectangleF(leftMargin, y, anchoRecom, alturaRecom));
        y += alturaRecom + espaciadoBloque;

        // Observaciones del Técnico (altura dinámica)
        if (!string.IsNullOrWhiteSpace(servicio.ComentariosInstalador))
        {
            var obsText = $"Durante nuestra visita, nuestro instalador {instalador.Nombre} ha observado que: {servicio.ComentariosInstalador}";
            float anchoObs = pageWidth - leftMargin - rightMargin;
            var sizeObs = valueFont.MeasureString(obsText, anchoObs);
            float alturaObs = sizeObs.Height + 8;

            NuevaPaginaSiNecesario(20 + alturaObs);
            page.Graphics.DrawString("Observaciones del Técnico", sectionFont, PdfBrushes.DarkBlue, new Syncfusion.Drawing.PointF(leftMargin, y));
            y += 20 + espaciadoLinea;
            page.Graphics.DrawString(obsText, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.RectangleF(leftMargin, y, anchoObs, alturaObs));
            y += alturaObs + espaciadoBloque;
        }

        // Nota Importante de Mantenimiento (altura dinámica)
        NuevaPaginaSiNecesario(20);
        page.Graphics.DrawString("Nota Importante de Mantenimiento", sectionFont, PdfBrushes.Orange, new Syncfusion.Drawing.PointF(leftMargin, y));
        y += 20 + espaciadoLinea;
        var notaText = "En caso de realizar algún mantenimiento en el circuito de calefacción donde se tenga que vaciar el contenido de agua, es necesario reponer los aditivos. Estos aditivos pueden ser comprados en www.lacasadelsueloradiante.es donde podrán asesorarle en caso de cualquier duda sobre el uso de los mismos.";
        float anchoNota = pageWidth - leftMargin - rightMargin;
        var sizeNota = valueFont.MeasureString(notaText, anchoNota);
        float alturaNota = sizeNota.Height + 8;
        NuevaPaginaSiNecesario(alturaNota);
        page.Graphics.DrawString(notaText, valueFont, PdfBrushes.Black, new Syncfusion.Drawing.RectangleF(leftMargin, y, anchoNota, alturaNota));
        y += alturaNota + espaciadoBloque;

        // Fotos de los valores
        var fotosValores = new List<(string Titulo, string Ruta)>();
        void AddFotoValor(string titulo, string? url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                var localPath = Path.Combine(FileSystem.AppDataDirectory, url);
                if (File.Exists(localPath))
                    fotosValores.Add((titulo, localPath));
            }
        }
        AddFotoValor("pH", servicio.FotoPhUrl);
        AddFotoValor("Conductividad", servicio.FotoConductividadUrl);
        AddFotoValor("Concentración", servicio.FotoConcentracionUrl);
        AddFotoValor("Turbidez", servicio.FotoTurbidezUrl);

        if (fotosValores.Count > 0)
        {
            NuevaPaginaSiNecesario(20);
            page.Graphics.DrawString("Fotos de los Valores", sectionFont, PdfBrushes.DarkBlue, new Syncfusion.Drawing.PointF(leftMargin, y));
            y += 20 + espaciadoLinea;

            const float imgSizeValor = 160;
            const float imgSpacingValor = 30;
            int fotosPorFila = 2;
            float totalWidth = fotosPorFila * imgSizeValor + (fotosPorFila - 1) * imgSpacingValor;
            float startX = leftMargin + (pageWidth - leftMargin - rightMargin - totalWidth) / 2;

            for (int i = 0; i < fotosValores.Count; i++)
            {
                int col = i % fotosPorFila;
                float x = startX + col * (imgSizeValor + imgSpacingValor);

                if (col == 0 && y + imgSizeValor + espaciadoFoto > pageHeight - bottomMargin)
                {
                    page = document.Pages.Add();
                    y = 0;
                }

                try
                {
                    using var imgStream = File.OpenRead(fotosValores[i].Ruta);
                    var image = new PdfBitmap(imgStream);
                    page.Graphics.DrawImage(image, new Syncfusion.Drawing.RectangleF(x, y, imgSizeValor, imgSizeValor));
                    page.Graphics.DrawString(fotosValores[i].Titulo, valueFont, PdfBrushes.Gray, new Syncfusion.Drawing.PointF(x, y + imgSizeValor + 4));
                }
                catch { }

                if (col == fotosPorFila - 1 || i == fotosValores.Count - 1)
                    y += imgSizeValor + espaciadoFoto;
            }
        }

        // Fotos de la instalación
        var fotosInstalacion = new List<(string Nombre, string Ruta)>();
        for (int i = 1; i <= 10; i++)
        {
            var prop = servicio.GetType().GetProperty($"FotoInstalacion{i}Url");
            if (prop != null)
            {
                var nombreArchivo = prop.GetValue(servicio) as string;
                if (!string.IsNullOrWhiteSpace(nombreArchivo))
                {
                    var localPath = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);
                    if (File.Exists(localPath))
                    {
                        fotosInstalacion.Add(($"Instalación {i}", localPath));
                    }
                }
            }
        }

        if (fotosInstalacion.Count > 0)
        {
            NuevaPaginaSiNecesario(20);
            page.Graphics.DrawString("Fotos de la Instalación", sectionFont, PdfBrushes.DarkBlue, new Syncfusion.Drawing.PointF(leftMargin, y));
            y += 20 + espaciadoLinea;

            const float imgSizeInst = 160;
            const float imgSpacingInst = 30;
            int fotosPorFila = 2;
            float totalWidth = fotosPorFila * imgSizeInst + (fotosPorFila - 1) * imgSpacingInst;
            float startX = leftMargin + (pageWidth - leftMargin - rightMargin - totalWidth) / 2;

            for (int i = 0; i < fotosInstalacion.Count; i++)
            {
                int col = i % fotosPorFila;
                float x = startX + col * (imgSizeInst + imgSpacingInst);

                if (col == 0 && y + imgSizeInst + espaciadoFoto > pageHeight - bottomMargin)
                {
                    page = document.Pages.Add();
                    y = 0;
                }

                try
                {
                    using var imgStream = File.OpenRead(fotosInstalacion[i].Ruta);
                    var image = new PdfBitmap(imgStream);
                    page.Graphics.DrawImage(image, new Syncfusion.Drawing.RectangleF(x, y, imgSizeInst, imgSizeInst));
                    page.Graphics.DrawString(fotosInstalacion[i].Nombre, valueFont, PdfBrushes.Gray, new Syncfusion.Drawing.PointF(x, y + imgSizeInst + 4));
                }
                catch { }

                if (col == fotosPorFila - 1 || i == fotosInstalacion.Count - 1)
                    y += imgSizeInst + espaciadoFoto;
            }
        }

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _lastNombreArchivo = null;
    }
}
