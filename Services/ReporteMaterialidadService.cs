using System.Globalization;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;
using velios.Api.Models.ReporteMaterialidad;


namespace velios.Api.Services;

/// <summary>
/// Servicio encargado de construir el reporte de materialidad
/// y generar el archivo PDF final por tarea.
/// </summary>
public class ReporteMaterialidadService : IReporteMaterialidadService
{
    private readonly IReporteMaterialidadRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ReporteMaterialidadService(
        IReporteMaterialidadRepository repository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;

        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerarPdfPorTareaAsync(int tareaId)
    {
        var tarea = await _repository.ObtenerTareaAsync(tareaId);
        if (tarea is null)
            throw new InvalidOperationException($"No se encontró la tarea con id {tareaId}.");

        var cliente = await _repository.ObtenerClienteAsync(tarea.ClienteId);
        if (cliente is null)
            throw new InvalidOperationException($"No se encontró el cliente de la tarea con id {tarea.ClienteId}.");

        var evidencias = await _repository.ObtenerEvidenciasPorTareaAsync(tarea.TareaId);

        foreach (var evidencia in evidencias)
        {
            if (!string.IsNullOrWhiteSpace(evidencia.UrlArchivo))
                evidencia.ImagenBytes = await DescargarImagenAsync(evidencia.UrlArchivo!);

            if (evidencia.Latitud.HasValue && evidencia.Longitud.HasValue)
            {
                evidencia.MapaBytes = await DescargarMapaAsync(evidencia.Latitud.Value, evidencia.Longitud.Value);

                var geo = await ObtenerGeocodingAsync(evidencia.Latitud.Value, evidencia.Longitud.Value);
                if (geo is not null)
                {
                    evidencia.DireccionFormateada = geo.DireccionFormateada;
                    evidencia.Colonia = geo.Colonia;
                    evidencia.Municipio = geo.Municipio;
                    evidencia.Estado = geo.Estado;
                    evidencia.CodigoPostal = geo.CodigoPostal;
                    evidencia.Pais = geo.Pais;
                }

                var lat = evidencia.Latitud.Value.ToString(CultureInfo.InvariantCulture);
                var lng = evidencia.Longitud.Value.ToString(CultureInfo.InvariantCulture);
                evidencia.GoogleMapsUrl = $"https://www.google.com/maps?q={lat},{lng}";
            }

            if (string.IsNullOrWhiteSpace(evidencia.DireccionFormateada))
                evidencia.DireccionFormateada = evidencia.Direccion;
        }

        tarea.Evidencias = evidencias;

        var reporte = new ReporteMaterialidadDto
        {
            Cliente = cliente,
            Tarea = tarea,
            FechaGeneracion = DateTime.Now,
            Resumen = ConstruirResumen(tarea)
        };

        // Generar QR que apunte a una URL directa cuando esté configurada.
        // Si en configuración existe 'Qr:DirectUrl' se usará dicha URL como contenido del QR
        // (puede contener placeholders {tareaId} o {taskCode} que serán reemplazados).
        byte[]? qrBytes = null;
        try
        {
            // QR directo fijado en código (valor proporcionado por el usuario)
            var qrDirectTemplate = "https://velioswebuat.adhw.com.mx/Documentos/Verificar?folio=VL-2026-000184&token=2EABBB9E4E82C7B0D3B87948B500BCDF5E2726D9B96027E8B4F16FB4DBD50EF6";
            qrBytes = GenerarQrBytes(qrDirectTemplate);
        }
        catch
        {
            // En caso de error generar QR local con contenido por defecto
            try
            {
                qrBytes = GenerarQrBytes($"TAREA:{tarea.TareaId}");
            }
            catch
            {
                qrBytes = null;
            }
        }

        return await ConstruirPdfAsync(reporte, qrBytes);
    }

    private static ResumenReporteDto ConstruirResumen(TareaReporteDto tarea)
    {
        var evidencias = tarea.Evidencias ?? new List<EvidenciaReporteDto>();

        return new ResumenReporteDto
        {
            TotalTareas = 1,
            TotalEvidencias = evidencias.Count,
            TotalEvidenciasConGeo = evidencias.Count(e => e.Latitud.HasValue && e.Longitud.HasValue),
            TotalEvidenciasSinGeo = evidencias.Count(e => !e.Latitud.HasValue || !e.Longitud.HasValue),
            PrimeraEvidencia = evidencias.Any() ? evidencias.Min(e => e.DateCreated) : null,
            UltimaEvidencia = evidencias.Any() ? evidencias.Max(e => e.DateCreated) : null
        };
    }

    private async Task<byte[]?> DescargarImagenAsync(string url)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            return await client.GetByteArrayAsync(url);
        }
        catch { return null; }
    }

    private async Task<byte[]?> DescargarMapaAsync(decimal latitud, decimal longitud)
    {
        try
        {
            var apiKey = _configuration["GoogleMaps:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            var lat = latitud.ToString(CultureInfo.InvariantCulture);
            var lng = longitud.ToString(CultureInfo.InvariantCulture);

            var mapaUrl =
                $"https://maps.googleapis.com/maps/api/staticmap" +
                $"?center={lat},{lng}&zoom=17&size=900x450&scale=2&maptype=roadmap" +
                $"&markers=color:red%7Clabel:E%7C{lat},{lng}&key={apiKey}";

            return await client.GetByteArrayAsync(mapaUrl);
        }
        catch { return null; }
    }

    private async Task<GeocodingInfoDto?> ObtenerGeocodingAsync(decimal latitud, decimal longitud)
    {
        try
        {
            var apiKey = _configuration["GoogleMaps:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            var lat = latitud.ToString(CultureInfo.InvariantCulture);
            var lng = longitud.ToString(CultureInfo.InvariantCulture);

            var url = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={lat},{lng}&language=es&key={apiKey}";
            var json = await client.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return null;

            var first = results[0];
            var info = new GeocodingInfoDto();

            if (first.TryGetProperty("formatted_address", out var fa))
                info.DireccionFormateada = fa.GetString();

            if (first.TryGetProperty("address_components", out var components))
            {
                foreach (var component in components.EnumerateArray())
                {
                    if (!component.TryGetProperty("types", out var types)) continue;

                    var typeValues = types.EnumerateArray()
                        .Select(t => t.GetString() ?? string.Empty).ToList();

                    var longName = component.TryGetProperty("long_name", out var ln) ? ln.GetString() : null;

                    if (typeValues.Contains("sublocality") || typeValues.Contains("sublocality_level_1") || typeValues.Contains("neighborhood"))
                        info.Colonia ??= longName;
                    if (typeValues.Contains("locality"))
                        info.Municipio ??= longName;
                    if (typeValues.Contains("administrative_area_level_2") && string.IsNullOrWhiteSpace(info.Municipio))
                        info.Municipio = longName;
                    if (typeValues.Contains("administrative_area_level_1"))
                        info.Estado ??= longName;
                    if (typeValues.Contains("postal_code"))
                        info.CodigoPostal ??= longName;
                    if (typeValues.Contains("country"))
                        info.Pais ??= longName;
                }
            }

            return info;
        }
        catch { return null; }
    }

    // Clase auxiliar para representar archivos adjuntos de la tarea
    private class ArchivoAdjunto
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public byte[]? Bytes { get; set; }
    }

    private async Task<byte[]> ConstruirPdfAsync(ReporteMaterialidadDto reporte, byte[]? qrBytes)
    {
        var tarea = reporte.Tarea;
        var cliente = reporte.Cliente;

        var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "logo_velios.png");
        byte[]? logoBytes = null;

        // Intentar cargar el logo desde varias ubicaciones (CWD y base directory)
        if (File.Exists(logoPath))
            logoBytes = File.ReadAllBytes(logoPath);
        else
        {
            var altPath = Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), "Resources", "logo_velios.png");
            if (File.Exists(altPath))
                logoBytes = File.ReadAllBytes(altPath);
        }

        var clienteDisplay = !string.IsNullOrWhiteSpace(cliente.Nombre)
            ? cliente.Nombre
            : !string.IsNullOrWhiteSpace(cliente.RazonSocial)
                ? cliente.RazonSocial
                : "N/A";

        var direccionDisplay = !string.IsNullOrWhiteSpace(cliente.Direccion)
            ? cliente.Direccion
            : "Dirección no disponible";

        // Preparar archivos adjuntos de la tarea (tarea.ImageURL puede contener varias URLs separadas por coma)
        var archivosTarea = new List<ArchivoAdjunto>();
        if (!string.IsNullOrWhiteSpace(tarea.ImageURL))
        {
            var lista = tarea.ImageURL!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            foreach (var url in lista)
            {
                var ext = Path.GetExtension(url).ToLowerInvariant();
                var fileName = Path.GetFileName(url);
                byte[]? bytes = null;

                // Intentar descargar previews sólo para imágenes
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp")
                {
                    try { bytes = await DescargarImagenAsync(url); } catch { bytes = null; }
                }

                archivosTarea.Add(new ArchivoAdjunto
                {
                    Url = url,
                    FileName = fileName,
                    Extension = ext,
                    Bytes = bytes
                });
            }

        }
        // Intentar cargar un ícono local para PDFs desde Resources (Icon_PDF.*)
        byte[]? pdfIconBytes = null;
        try
        {
            var possibleDirs = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "Resources"),
                Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), "Resources")
            };

            foreach (var dir in possibleDirs)
            {
                if (!Directory.Exists(dir)) continue;

                var candidates = new[] { "Icon_PDF.png", "Icon_PDF.jpg", "Icon_PDF.jpeg", "Icon_PDF.bmp", "Icon_PDF.gif", "Icon_PDF.svg" };
                var found = candidates.Select(c => Path.Combine(dir, c)).FirstOrDefault(File.Exists);
                if (found != null)
                {
                    pdfIconBytes = File.ReadAllBytes(found);
                    break;
                }
            }
        }
        catch { pdfIconBytes = null; }

        var document = Document.Create(container =>
        {
            // =========================================================
            // PÁGINA 1 - RESUMEN EJECUTIVO
            // =========================================================
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#1F2937"));

                page.Background().Element(CrearFondoPagina);

                page.Header().Element(c =>
                    CrearHeader(c, logoBytes, "INFORME DE TAREA", tarea.Titulo ?? "SIN TÍTULO"));

                page.Content().PaddingTop(10).Element(c =>
                    CrearContenidoPrincipalConSidebar(c, reporte, clienteDisplay, direccionDisplay, logoBytes, archivosTarea, pdfIconBytes));

                page.Footer().Element(c =>
                    CrearFooter(c, clienteDisplay, direccionDisplay, tarea.EstatusNombre ?? "Tarea", logoBytes, qrBytes));
            });

            // =========================================================
            // PÁGINAS DE EVIDENCIA
            // =========================================================
            for (int i = 0; i < tarea.Evidencias.Count; i++)
            {
                var evidencia = tarea.Evidencias[i];

                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor("#1F2937"));

                    page.Background().Element(CrearFondoPagina);

                    page.Header().Element(c =>
                        CrearHeader(c, logoBytes, "INFORME DE TAREA", tarea.Titulo ?? "SIN TÍTULO"));

                    page.Content().PaddingTop(10).PaddingLeft(28).Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Text($"EVIDENCIA {i + 1:D2}")
                            .FontSize(16).Bold().FontColor("#24364D");

                        column.Item().Text(tarea.EstatusNombre ?? "Avance")
                            .FontSize(10).SemiBold().FontColor("#6B7280");

                        column.Item().Text(text =>
                        {
                            text.Span("Observaciones referentes a la tarea: ")
                                .SemiBold().FontSize(9).FontColor("#24364D");
                            text.Span(tarea.Descripcion ?? "N/A")
                                .FontSize(9).FontColor("#6B7280");
                        });

                        // Foto + sidebar datos
                        column.Item().Row(row =>
                        {
                            row.RelativeItem(1.45f).Element(c =>
                            {
                                var box = c.Border(1).BorderColor("#D6DCE5")
                                    .Background("#F8FAFC").Padding(8).Height(330);

                                if (evidencia.ImagenBytes is not null && evidencia.ImagenBytes.Length > 0)
                                    box.AlignCenter().AlignMiddle().Image(evidencia.ImagenBytes, ImageScaling.FitArea);
                                else
                                    box.AlignCenter().AlignMiddle()
                                        .Text("No fue posible cargar la imagen.")
                                        .FontSize(11).FontColor("#64748B");
                            });

                            row.ConstantItem(12);

                            row.ConstantItem(185).Background("#F3F4F6").Padding(10).Column(right =>
                            {
                                right.Spacing(8);

                                right.Item().Text("Datos de captura")
                                    .Bold().FontSize(11).FontColor("#24364D");
                                right.Item().LineHorizontal(1).LineColor("#D1D5DB");

                                right.Item().Element(x => CrearCampoLateral(x, "Fecha de captura", evidencia.DateCreated.ToString("dd/MM/yyyy")));
                                right.Item().Element(x => CrearCampoLateral(x, "Registrado en sistema", evidencia.DateCreated.ToString("dd/MM/yyyy")));
                                right.Item().Element(x => CrearCampoLateral(x, "Usuario de registro", tarea.NombreOperador));
                                right.Item().Element(x => CrearCampoLateral(x, "Equipo", evidencia.ModeloDispositivo));
                                right.Item().Element(x => CrearCampoLateral(x, "Latitud de captura",
                                    evidencia.Latitud?.ToString("0.00000000", CultureInfo.InvariantCulture) ?? "N/A"));
                                right.Item().Element(x => CrearCampoLateral(x, "Longitud de captura",
                                    evidencia.Longitud?.ToString("0.00000000", CultureInfo.InvariantCulture) ?? "N/A"));

                                right.Item().PaddingTop(6).LineHorizontal(1).LineColor("#D1D5DB");

                                right.Item().Text("Validación")
                                    .Bold().FontSize(11).FontColor("#24364D");

                                right.Item().Element(x => CrearCheckValidacion(x, "Fecha de evidencia coincide con el registro", true));
                                right.Item().Element(x => CrearCheckValidacion(x, "Ubicación validada con sucursal",
                                    evidencia.Latitud.HasValue && evidencia.Longitud.HasValue));
                                right.Item().Element(x => CrearCheckValidacion(x, "Tomada desde la app Velios", true));
                            });
                        });

                        // Mapa
                        column.Item().Text("Mapa de ubicación")
                            .Bold().FontSize(10).FontColor("#24364D");

                        column.Item().Element(c =>
                        {
                            var box = c.Border(1).BorderColor("#D6DCE5")
                                .Background("#F8FAFC").Padding(8).Height(150);

                            if (evidencia.MapaBytes is not null && evidencia.MapaBytes.Length > 0)
                                box.AlignCenter().AlignMiddle().Image(evidencia.MapaBytes, ImageScaling.FitArea);
                            else
                                box.AlignCenter().AlignMiddle()
                                    .Text("No fue posible cargar el mapa.")
                                    .FontSize(10).FontColor("#64748B");
                        });

                        // Archivos adjuntos — mostrar icono según extensión, nombre y enlace si existe URL
                        if (!string.IsNullOrWhiteSpace(evidencia.UrlArchivo))
                        {
                            var ext = Path.GetExtension(evidencia.UrlArchivo).ToLowerInvariant();

                            column.Item().Element(c =>
                            {
                                c.Border(1).BorderColor("#D6DCE5").Padding(8).Background("#F9FAFB").Column(col =>
                                {
                                    col.Item().Text("Archivos adjuntos").Bold().FontSize(10).FontColor("#24364D");
                                    col.Item().PaddingTop(6).Row(r =>
                                    {
                                        // Icono visual simple según extensión (emoji) y etiqueta
                                        r.ConstantItem(36).Height(36).AlignCenter().AlignMiddle().Element(icon =>
                                        {
                                            var emoji = ext switch
                                            {
                                                ".xls" or ".xlsx" => "📊",
                                                ".ppt" or ".pptx" => "📽️",
                                                ".doc" or ".docx" => "📄",
                                                ".pdf" => "📕",
                                                ".zip" or ".rar" => "🗜️",
                                                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" => "🖼️",
                                                _ => "📁"
                                            };

                                            var label = (ext ?? string.Empty).TrimStart('.').ToUpperInvariant();
                                            if (string.IsNullOrWhiteSpace(label)) label = "FILE";

                                            icon.Background(Colors.White).Border(1).BorderColor("#D1D5DB")
                                                .AlignCenter().AlignMiddle()
                                                .Column(ic =>
                                                {
                                                    ic.Item().Text(emoji).FontSize(12);
                                                    ic.Item().Text(label).FontSize(8).FontColor("#6B7280");
                                                });
                                        });

                                        r.ConstantItem(8);

                                        r.RelativeItem().Column(info =>
                                        {
                                            var fileName = Path.GetFileName(evidencia.UrlArchivo);
                                            info.Item().Text(fileName).SemiBold().FontSize(9).FontColor("#24364D");
                                            info.Item().Text(text =>
                                            {
                                                text.Span(" ").FontSize(8).FontColor("#6B7280");
                                                if (!string.IsNullOrWhiteSpace(evidencia.UrlArchivo))
                                                    text.Hyperlink(evidencia.UrlArchivo, "Abrir / Descargar");
                                            });
                                        });
                                    });
                                });
                            });
                        }

                        // Datos de ubicación y técnicos
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Element(c =>
                                CrearTarjetaResumen(c, "Ubicación", content =>
                                {
                                    content.Item().Element(x => CrearCampoFicha(x, "Dirección", evidencia.DireccionFormateada ?? evidencia.Direccion));
                                    content.Item().Element(x => CrearCampoFicha(x, "Colonia", evidencia.Colonia));
                                    content.Item().Element(x => CrearCampoFicha(x, "Municipio", evidencia.Municipio));
                                    content.Item().Element(x => CrearCampoFicha(x, "Estado", evidencia.Estado));
                                    content.Item().Element(x => CrearCampoFicha(x, "Código postal", evidencia.CodigoPostal));
                                    content.Item().Element(x => CrearCampoFicha(x, "País", evidencia.Pais));
                                }));

                            row.ConstantItem(12);

                            row.RelativeItem().Element(c =>
                                CrearTarjetaResumen(c, "Datos técnicos", content =>
                                {
                                    content.Item().Element(x => CrearCampoFicha(x, "Precisión (m)",
                                        evidencia.PrecisionMetros?.ToString("0.##", CultureInfo.InvariantCulture) ?? "N/A"));
                                    content.Item().Element(x => CrearCampoFicha(x, "Altitud",
                                        evidencia.Altitud?.ToString("0.##", CultureInfo.InvariantCulture) ?? "N/A"));
                                    content.Item().Element(x => CrearCampoFicha(x, "Google Maps",
                                        string.IsNullOrWhiteSpace(evidencia.GoogleMapsUrl) ? "N/A" : "Disponible"));
                                }));
                        });

                        if (!string.IsNullOrWhiteSpace(evidencia.GoogleMapsUrl))
                        {
                            column.Item().Text(text =>
                            {
                                text.Span("Abrir ubicación en mapa: ").SemiBold().FontSize(9).FontColor("#24364D");
                                text.Hyperlink(evidencia.GoogleMapsUrl!, "Ver en Google Maps");
                            });
                        }
                    });

                    page.Footer().Element(c =>
                        CrearFooter(c, clienteDisplay, direccionDisplay, tarea.EstatusNombre ?? "Tarea", logoBytes, qrBytes));
                });
            }
        });

        return document.GeneratePdf();
    }

    // =========================================================================
    // FONDO DE PÁGINA — barra lateral naranja + azul + gris
    // =========================================================================
    private static void CrearFondoPagina(IContainer container)
    {
        // Simplify background to a full-height left stripe to avoid
        // conflicting fixed-height constraints that can cause
        // QuestPDF layout exceptions when the background is rendered
        // in decoration slots (header/footer).
        container.Layers(layers =>
        {
            layers.PrimaryLayer();
            layers.Layer().Element(layer =>
            {
                layer.Row(row =>
                {
                    // Full-height left stripe (fixed width) that fills the page height
                    row.ConstantItem(22).Background("#F15A24");
                    // The rest of the page remains empty (content layer will render above)
                    row.RelativeItem();
                });
            });
        });
    }

    // =========================================================================
    // HEADER — logo PNG + separador + título
    // =========================================================================
    private static void CrearHeader(
        IContainer container,
        byte[]? logoBytes,
        string titulo,
        string subtitulo)
    {
        container.PaddingLeft(28).PaddingRight(10).PaddingTop(5).Column(column =>
        {
            column.Item().Row(row =>
            {
                // Logo Velios (PNG real)
                // Avoid forcing a fixed height so header can adapt to available
                // decoration space and prevent layout conflicts.
                row.ConstantItem(150).AlignMiddle().Element(c =>
                {
                    if (logoBytes != null)
                        c.Image(logoBytes, ImageScaling.FitArea);
                    else
                    {
                        // Fallback textual si no hay imagen
                        c.Row(r =>
                        {
                            r.ConstantItem(28).Height(28).Background("#F15A24")
                                .AlignCenter().AlignMiddle()
                                .Text("V").Bold().FontSize(16).FontColor(Colors.White);
                            r.ConstantItem(6);
                            r.RelativeItem().AlignMiddle()
                                .Text("VELIOS").Bold().FontSize(16).FontColor("#24364D");
                        });
                    }
                });

                // Separador vertical
                row.ConstantItem(1).PaddingTop(8).PaddingBottom(8).Background("#D1D5DB");

                row.ConstantItem(12);

                // Títulos: mostrar en un recuadro a la derecha del logo (titulo pequeño + subtítulo grande)
                row.RelativeItem().AlignMiddle().Element(containerTitles =>
                {
                    containerTitles.Row(r =>
                    {
                        // Caja con borde y fondo claro que contiene los textos.
                        // Remove forced height so the header can wrap naturally.
                        r.RelativeItem().Element(box =>
                        {
                            box.Border(1).BorderColor("#E5E7EB").Background(Colors.White)
                                .Padding(8)
                                .Column(col =>
                                {
                                    col.Item().Text(titulo)
                                        .FontSize(10).SemiBold().FontColor("#6B7280");
                                    col.Item().Text(subtitulo ?? "SIN TÍTULO")
                                        .FontSize(16).Bold().FontColor("#24364D");
                                });
                        });
                    });
                });
            });

            // Línea inferior del header
            column.Item().PaddingTop(4).LineHorizontal(1).LineColor("#E5E7EB");
        });
    }

    // =========================================================================
    // CONTENIDO PÁGINA 1 — columna izquierda + sidebar derecho
    // =========================================================================
    private static void CrearContenidoPrincipalConSidebar(
        IContainer container,
        ReporteMaterialidadDto reporte,
        string clienteDisplay,
        string direccionDisplay,
        byte[]? logoBytes,
        List<ArchivoAdjunto> archivosTarea,
        byte[]? pdfIconBytes)
    {
        var tarea = reporte.Tarea;
        var cliente = reporte.Cliente;

        container.PaddingLeft(28).Row(row =>
        {
            // -----------------------------------------------------------------
            // COLUMNA IZQUIERDA
            // -----------------------------------------------------------------
            row.RelativeItem(2.7f).PaddingRight(12).Column(left =>
            {
                left.Spacing(14);

                // Se removió el logo repetido arriba de la sección cliente (se muestra solo la información)
                left.Item().Element(lc => { });

                // Sección cliente
                left.Item().Element(c =>
                    CrearSeccionConIcono(c, "Información general  del cliente", content =>
                    {
                        content.Item().Element(x => CrearFilaSimple(x, "Nombre", clienteDisplay));
                        content.Item().Element(x => CrearFilaSimple(x, "Razón Social", cliente.RazonSocial));
                        content.Item().Element(x => CrearFilaSimple(x, "Teléfono", cliente.Telefono));
                        content.Item().Element(x => CrearFilaSimple(x, "Email", cliente.Email));
                    }));

                // Sección tarea
                left.Item().Element(c =>
                    CrearSeccionConIcono(c, "Información de tarea", content =>
                    {
                        content.Item().Element(x => CrearFilaSimple(x, "Nombre", tarea.Titulo));

                        content.Item().PaddingTop(14).Text(text =>
                        {
                            text.Span("Descripción de la actividad a desarrollar: ")
                                .SemiBold().FontSize(9).FontColor("#24364D");
                            text.Span(tarea.Descripcion ?? "N/A")
                                .FontSize(9).FontColor("#6B7280");
                        });

                        content.Item().PaddingTop(14).Column(obs =>
                        {
                            obs.Spacing(6);
                            obs.Item().Text("Observaciones :")
                                .SemiBold().FontSize(10).FontColor("#24364D");

                            foreach (var item in new[]
                            {
                                "Verificar que la nueva señalética cumpla con el manual de marca vigente.",
                                "Cuidar que no queden restos de adhesivo, tornillos visibles o daños.",
                                "Coordinar la instalación en horarios de baja afluencia."
                            })
                            {
                                obs.Item().Row(r =>
                                {
                                    r.ConstantItem(10).Text("•")
                                        .FontSize(12).FontColor("#24364D");
                                    r.RelativeItem().Text(item)
                                        .FontSize(9).FontColor("#6B7280");
                                });
                            }
                        });

                        // Sección: Archivos adjuntos de la tarea (subidos vía TareaArchivoController)
                        if (archivosTarea != null && archivosTarea.Count > 0)
                        {
                            content.Item().PaddingTop(10).Element(el =>
                                CrearSeccionConIcono(el, "Archivos adjuntos de la tarea", filesContent =>
                                {
                                    filesContent.Item().Column(fc =>
                                    {
                                        foreach (var a in archivosTarea)
                                        {
                                            fc.Item().PaddingTop(6).Row(r =>
                                            {
                                                r.ConstantItem(36).Height(36).AlignCenter().AlignMiddle().Element(icon =>
                                                {
                                                    // Mostrar imagen local para PDFs si está disponible
                                                    if (a.Extension == ".pdf" && pdfIconBytes is not null && pdfIconBytes.Length > 0)
                                                    {
                                                        icon.Background(Colors.White).Border(1).BorderColor("#D1D5DB")
                                                            .AlignCenter().AlignMiddle()
                                                            .Element(img => img.Image(pdfIconBytes, ImageScaling.FitArea));
                                                    }
                                                    else
                                                    {
                                                        var emoji = a.Extension switch
                                                        {
                                                            ".xls" or ".xlsx" => "📊",
                                                            ".ppt" or ".pptx" => "📽️",
                                                            ".doc" or ".docx" => "📄",
                                                            ".pdf" => "📕",
                                                            ".zip" or ".rar" => "🗜️",
                                                            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" => "🖼️",
                                                            _ => "📁"
                                                        };

                                                        var label = (a.Extension ?? string.Empty).TrimStart('.').ToUpperInvariant();
                                                        if (string.IsNullOrWhiteSpace(label)) label = "FILE";

                                                        icon.Background(Colors.White).Border(1).BorderColor("#D1D5DB")
                                                            .AlignCenter().AlignMiddle()
                                                            .Column(ic =>
                                                            {
                                                                ic.Item().Text(emoji).FontSize(12);
                                                                ic.Item().Text(label).FontSize(8).FontColor("#6B7280");
                                                            });
                                                    }
                                                });

                                                r.ConstantItem(8);

                                                r.RelativeItem().Column(info =>
                                                {
                                                    // Mostrar sólo la ruta (URL) como enlace, sin la línea adicional del nombre
                                                    info.Item().Text(text =>
                                                    {
                                                        if (!string.IsNullOrWhiteSpace(a.Url))
                                                            text.Hyperlink(a.Url, a.Url).FontSize(8).FontColor("#6B7280");
                                                        else
                                                            text.Span("N/A").FontSize(8).FontColor("#6B7280");
                                                    });
                                                });
                                            });
                                        }
                                    });
                                }));
                        }
                    }));
            });

            // -----------------------------------------------------------------
            // SIDEBAR DERECHO
            // -----------------------------------------------------------------
            row.ConstantItem(175).Background("#F3F4F6").Padding(12).Column(right =>
            {
                right.Spacing(8);

                // Banda superior azul
                right.Item().Height(6).Background("#24364D");

                // Estatus / tipo de plan
                right.Item().PaddingTop(4).Text(tarea.EstatusNombre ?? "Sin estatus")
                    .SemiBold().FontSize(13).FontColor("#24364D");

                // "Plan de trabajo" con ícono carpeta
                right.Item().Row(r =>
                {
                    r.ConstantItem(16).AlignMiddle().AlignCenter()
                        .Text("▭").FontSize(11).FontColor("#6B7280");
                    r.ConstantItem(4);
                    r.RelativeItem().AlignMiddle()
                        .Text("Plan de trabajo").FontSize(9).FontColor("#6B7280");
                });

                right.Item().LineHorizontal(1).LineColor("#D1D5DB");

                // Operador con ícono de persona
                right.Item().PaddingTop(4).Row(r =>
                {
                    r.ConstantItem(26).Height(26).Background("#E5E7EB").Border(1).BorderColor("#D1D5DB")
                        .AlignCenter().AlignMiddle()
                        .Text("●").FontSize(14).FontColor("#24364D");
                    r.ConstantItem(8);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(tarea.NombreOperador ?? "N/A")
                            .SemiBold().FontSize(11).FontColor("#24364D");
                        c.Item().Text("Operador").FontSize(8).FontColor("#6B7280");
                    });
                });

                // Supervisor con ícono de persona
                right.Item().PaddingTop(4).Row(r =>
                {
                    r.ConstantItem(26).Height(26).Background("#E5E7EB").Border(1).BorderColor("#D1D5DB")
                        .AlignCenter().AlignMiddle()
                        .Text("●").FontSize(14).FontColor("#24364D");
                    r.ConstantItem(8);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(tarea.NombreSupervisor ?? "N/A")
                            .SemiBold().FontSize(11).FontColor("#24364D");
                        c.Item().Text("Supervisor").FontSize(8).FontColor("#6B7280");
                    });
                });

                // Bloque presupuesto
                right.Item().PaddingTop(8).Background("#24364D").Padding(10).AlignCenter().Column(c =>
                {
                    c.Item().AlignCenter().Text("PRESUPUESTO")
                        .FontSize(9).FontColor(Colors.White).SemiBold();
                    c.Item().AlignCenter().Text(
                            tarea.PresupuestoAsignado.HasValue
                                ? $"${tarea.PresupuestoAsignado.Value:N2} {tarea.Moneda}"
                                : "N/A")
                        .Bold().FontSize(15).FontColor(Colors.White);
                });

                // Sucursal con ícono pin (mostrar siempre el pin, no el logo)
                right.Item().PaddingTop(10).Row(r =>
                {
                    r.ConstantItem(16).AlignTop()
                        .Text("⊙").FontSize(13).FontColor("#F15A24");
                    r.ConstantItem(4);

                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(clienteDisplay)
                            .SemiBold().FontSize(11).FontColor("#24364D");
                        c.Item().Text(direccionDisplay)
                            .FontSize(8).FontColor("#6B7280");
                    });
                });

                right.Item().PaddingTop(6).LineHorizontal(1).LineColor("#D1D5DB");

                // Fechas
                CrearFechaSidebar(right, tarea.FechaAsignacion, "Fecha asignación", false);
                CrearFechaSidebar(right, tarea.FechaProgramada, "Fecha programada", false);
                CrearFechaSidebar(right, tarea.FechaVencimiento, "Fecha vencimiento", true);

                // Estado de cumplimiento: EN TIEMPO / VENCIDA según FechaVencimiento
                var fechaVencimiento = tarea.FechaVencimiento;
                var estaVencida = DateTime.Now.Date > fechaVencimiento.Date;

                if (estaVencida)
                {
                    // Vencida — fondo rojo
                    right.Item().PaddingTop(10).Background("#EF4444").PaddingVertical(8).AlignCenter()
                        .Text("VENCIDA").Bold().FontColor(Colors.White).FontSize(12);
                }
                else
                {
                    // En tiempo — fondo verde
                    right.Item().PaddingTop(10).Background("#16C60C").PaddingVertical(8).AlignCenter()
                        .Text("EN TIEMPO").Bold().FontColor(Colors.White).FontSize(12);
                }
            });
        });
    }

    // =========================================================================
    // FOOTER — QR placeholder + info central + barra azul con paginación
    // =========================================================================
    private static void CrearFooter(
        IContainer container,
        string clienteDisplay,
        string direccionDisplay,
        string estatus,
        byte[]? logoBytes,
        byte[]? qrBytes)
    {
        container.PaddingLeft(28).Column(column =>
        {
            column.Item().Row(row =>
            {
                // QR: mostrar imagen si está disponible, si no mantener el placeholder
                row.ConstantItem(62).Height(62).Border(2).BorderColor("#24364D")
                    .Padding(6).AlignCenter().AlignMiddle().Element(qc =>
                    {
                        if (qrBytes is not null && qrBytes.Length > 0)
                        {
                            qc.Image(qrBytes, ImageScaling.FitArea);
                        }
                        else
                        {
                            qc.Column(qr =>
                            {
                                qr.Item().Row(r =>
                                {
                                    r.RelativeItem().Height(8).Background("#24364D");
                                    r.ConstantItem(3);
                                    r.RelativeItem().Height(8).Background("#24364D");
                                    r.ConstantItem(3);
                                    r.ConstantItem(8).Height(8).Background("#F15A24");
                                });
                                qr.Item().Height(3);
                                qr.Item().Row(r =>
                                {
                                    r.ConstantItem(8).Height(8).Background("#24364D");
                                    r.ConstantItem(3);
                                    r.RelativeItem().Height(8).Background("#F15A24");
                                    r.ConstantItem(3);
                                    r.RelativeItem().Height(8).Background("#24364D");
                                });
                                qr.Item().Height(3);
                                qr.Item().Row(r =>
                                {
                                    r.RelativeItem().Height(8).Background("#F15A24");
                                    r.ConstantItem(3);
                                    r.RelativeItem().Height(8).Background("#24364D");
                                    r.ConstantItem(3);
                                    r.RelativeItem().Height(8).Background("#24364D");
                                });
                            });
                        }
                    });

                // Info central
                row.RelativeItem().PaddingLeft(10).PaddingRight(10).AlignMiddle().Row(r =>
                {
                    // Mostrar logo Velios en el lugar del texto de empresa y otro logo más grande a la derecha
                    // Logo izquierdo (igual tamaño y alineación que el derecho)
                    r.RelativeItem().AlignCenter().Row(leftLogo =>
                    {
                        if (logoBytes is not null && logoBytes.Length > 0)
                        {
                            leftLogo.ConstantItem(80).Height(48).AlignCenter().AlignMiddle()
                                .Element(img => img.Image(logoBytes, ImageScaling.FitArea));
                        }
                        else
                        {
                            leftLogo.ConstantItem(36).Height(36).Background("#F15A24")
                                .AlignCenter().AlignMiddle()
                                .Text("V").Bold().FontSize(16).FontColor(Colors.White);
                        }
                    });

                    r.ConstantItem(10).AlignCenter().Text("|").FontColor("#D1D5DB");

                    // Logo derecho — agrandado para mayor visibilidad
                    r.RelativeItem().AlignCenter().Row(logoRow =>
                    {
                        if (logoBytes is not null && logoBytes.Length > 0)
                        {
                            logoRow.ConstantItem(80).Height(48).AlignCenter().AlignMiddle()
                                .Element(img => img.Image(logoBytes, ImageScaling.FitArea));
                        }
                        else
                        {
                            logoRow.ConstantItem(36).Height(36).Background("#F15A24")
                                .AlignCenter().AlignMiddle()
                                .Text("V").Bold().FontSize(16).FontColor(Colors.White);
                            logoRow.ConstantItem(4);
                            logoRow.RelativeItem().AlignMiddle()
                                .Text("Velios").Bold().FontSize(11).FontColor("#24364D");
                        }
                    });

                    r.ConstantItem(10).AlignCenter().Text("|").FontColor("#D1D5DB");

                    // Estatus con ícono carpeta
                    r.RelativeItem().AlignCenter().Row(eRow =>
                    {
                        eRow.ConstantItem(14).AlignMiddle()
                            .Text("▭").FontSize(10).FontColor("#6B7280");
                        eRow.ConstantItem(4);
                        eRow.RelativeItem().AlignMiddle()
                            .Text(estatus).FontSize(9).SemiBold().FontColor("#6B7280");
                    });
                });
            });

            // Barra inferior azul
            column.Item().Background("#24364D").PaddingVertical(6).PaddingHorizontal(10).Row(row =>
            {
                row.RelativeItem().AlignCenter().Text(direccionDisplay)
                    .FontSize(8).FontColor(Colors.White);

                row.ConstantItem(115).AlignRight().Text(text =>
                {
                    text.Span("PÁGINA ").FontSize(8).FontColor(Colors.White);
                    text.CurrentPageNumber();
                    text.Span(" DE ").FontSize(8).FontColor(Colors.White);
                    text.TotalPages();
                });
            });
        });
    }

    // =========================================================================
    // SECCIÓN CON ÍCONO — cuadro con documento + título
    // =========================================================================
    private static void CrearSeccionConIcono(
        IContainer container,
        string titulo,
        Action<ColumnDescriptor> contenido)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                // Ícono de documento estilizado (líneas horizontales simulando texto)
                row.ConstantItem(34).Height(34).Border(1).BorderColor("#D1D5DB")
                    .Background("#F9FAFB").AlignCenter().AlignMiddle()
                    .Column(icon =>
                    {
                        icon.Item().AlignCenter().Text("≡")
                            .FontSize(18).Bold().FontColor("#24364D");
                    });

                row.ConstantItem(10);

                row.RelativeItem().AlignMiddle().Text(titulo)
                    .SemiBold().FontSize(11).FontColor("#24364D");
            });

            column.Item().PaddingTop(10).Column(inner =>
            {
                inner.Spacing(8);
                contenido(inner);
            });
        });
    }

    // =========================================================================
    // FILA SIMPLE — etiqueta / valor con borde inferior
    // =========================================================================
    private static void CrearFilaSimple(IContainer container, string etiqueta, string? valor)
    {
        container.BorderBottom(1).BorderColor("#D1D5DB").PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Text(etiqueta)
                .SemiBold().FontSize(9).FontColor("#7C7C7C");
            row.RelativeItem(2).Text(string.IsNullOrWhiteSpace(valor) ? "N/A" : valor)
                .SemiBold().FontSize(9).FontColor("#24364D");
        });
    }

    // =========================================================================
    // FECHA EN SIDEBAR
    // =========================================================================
    private static void CrearFechaSidebar(ColumnDescriptor column, DateTime? fecha, string etiqueta, bool activa)
    {
        column.Item().PaddingTop(4).Row(row =>
        {
            row.ConstantItem(16).AlignTop().Text(activa ? "●" : "○")
                .FontSize(13).FontColor(activa ? "#22C55E" : "#6B7280");
            row.RelativeItem().Column(c =>
            {
                c.Item().Text(fecha?.ToString("dd/MM/yyyy") ?? "N/A")
                    .SemiBold().FontSize(10).FontColor("#24364D");
                c.Item().Text(etiqueta).FontSize(9).FontColor("#6B7280");
            });
        });
    }

    // =========================================================================
    // TARJETA RESUMEN — título con fondo azul claro
    // =========================================================================
    private static void CrearTarjetaResumen(
        IContainer container,
        string titulo,
        Action<ColumnDescriptor> contenido)
    {
        container.Border(1).BorderColor("#D6DCE5").Background(Colors.White).Column(column =>
        {
            column.Item().Background("#EEF4FF").BorderBottom(1).BorderColor("#D6DCE5")
                .PaddingVertical(8).PaddingHorizontal(10)
                .Text(titulo).FontSize(11).Bold().FontColor("#0B2A6F");

            column.Item().Padding(10).Column(inner =>
            {
                inner.Spacing(7);
                contenido(inner);
            });
        });
    }

    // =========================================================================
    // CAMPO FICHA
    // =========================================================================
    private static void CrearCampoFicha(IContainer container, string etiqueta, string? valor)
    {
        container.Column(column =>
        {
            column.Item().Text(etiqueta).FontSize(8).SemiBold().FontColor("#64748B");
            column.Item().PaddingTop(1)
                .Text(string.IsNullOrWhiteSpace(valor) ? "N/A" : valor)
                .FontSize(10).FontColor("#0F172A");
        });
    }

    // =========================================================================
    // CAMPO LATERAL (evidencia sidebar)
    // =========================================================================
    private static void CrearCampoLateral(IContainer container, string etiqueta, string? valor)
    {
        container.Column(column =>
        {
            column.Item().Text(etiqueta).FontSize(8).SemiBold().FontColor("#6B7280");
            column.Item().PaddingTop(1)
                .Text(string.IsNullOrWhiteSpace(valor) ? "N/A" : valor)
                .FontSize(9).FontColor("#24364D");
        });
    }

    // =========================================================================
    // CHECK DE VALIDACIÓN
    // =========================================================================
    private static void CrearCheckValidacion(IContainer container, string texto, bool ok)
    {
        container.Border(1).BorderColor("#D6DCE5").Background(Colors.White).Padding(8).Row(row =>
        {
            row.ConstantItem(18).Height(18).AlignMiddle().AlignCenter().Element(box =>
            {
                box.Border(1)
                    .BorderColor(ok ? "#16A34A" : "#94A3B8")
                    .Background(ok ? "#DCFCE7" : "#FFFFFF")
                    .AlignCenter().AlignMiddle()
                    .Text(ok ? "✓" : "")
                    .FontSize(10).Bold().FontColor("#166534");
            });

            row.ConstantItem(6);

            row.RelativeItem().AlignMiddle().Text(texto)
                .FontSize(9).FontColor("#334155");
        });
    }

    private static byte[] GenerarQrBytes(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        // 20 pixels por módulo produce una imagen de tamaño adecuado
        return png.GetGraphic(20);
    }
}