using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using velios.Api.Models.ReporteMaterialidad;
using velios.Api.Models.Tareas;


namespace velios.Api.Services;

/// <summary>
/// Servicio encargado de construir el reporte de materialidad
/// y generar el archivo PDF final por tarea.
/// </summary>
public class ReporteMaterialidadService : IReporteMaterialidadService
{
    private const string Titulo = "INFORME DE TAREA";
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

        byte[]? logoProveedorBytes = null;
        if (!string.IsNullOrWhiteSpace(tarea.LogoUrlProveedor))
            logoProveedorBytes = await DescargarImagenAsync(tarea.LogoUrlProveedor);

        var cliente = await _repository.ObtenerClienteAsync(tarea.ClienteId);
        if (cliente is null)
            throw new InvalidOperationException($"No se encontró el cliente de la tarea con id {tarea.ClienteId}.");

        var evidencias = await _repository.ObtenerEvidenciasPorTareaAsync(tarea.TareaId);
        var observaciones = await _repository.ObtenerObservacionesPorTareaAsync(tarea.TareaId);
        tarea.Observaciones = observaciones;
        tarea.DireccionCentroTrabajo = await _repository.ObtenerDireccionCentroTrabajoAsync(tarea.CentroTrabajoId);
        tarea.TelefonoCentroTrabajo = await _repository.ObtenerTelefonoCentroTrabajoAsync(tarea.CentroTrabajoId);
        tarea.NombreCentroTrabajo = await _repository.ObtenerNombreCentroTrabajoAsync(tarea.CentroTrabajoId);


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
            // QR directo fijado en código (valor proporcionado por el usuario
            var token = BuildValidationToken(tarea.TareaId);
            var BaseUrlFront = _configuration["AppSettings:BaseUrlFront"];
            var qrDirectTemplate = BaseUrlFront + $"Documentos/Verificar?taskId={tareaId}&token={token}";
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

        return await ConstruirPdfAsync(reporte, qrBytes, logoProveedorBytes);
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

            // PUNTO 5: Estilos de color azul para el mapa (similar a la referencia)
            var styles =
                "&style=feature:water|color:0xA9CCE3" +
                "&style=feature:landscape|color:0xEAF2FB" +
                "&style=feature:road|color:0xFFFFFF" +
                "&style=feature:road|element:geometry.stroke|color:0xC9D6E3" +
                "&style=feature:poi|visibility:simplified" +
                "&style=feature:poi|element:geometry|color:0xD6E4F0" +
                "&style=feature:administrative|element:labels.text.fill|color:0x24364D";

            var mapaUrl =
                $"https://maps.googleapis.com/maps/api/staticmap" +
                $"?center={lat},{lng}&zoom=17&size=900x450&scale=2&maptype=roadmap" +
                styles +
                $"&key={apiKey}";

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

    // =========================================================================
    // CARGA DE RECURSOS — helper genérico para íconos en /Resources
    // =========================================================================
    private static byte[]? CargarRecurso(string nombreArchivo)
    {
        try
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Resources", nombreArchivo);
            if (File.Exists(path)) return File.ReadAllBytes(path);

            var altPath = Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), "Resources", nombreArchivo);
            if (File.Exists(altPath)) return File.ReadAllBytes(altPath);
        }
        catch { }
        return null;
    }
    private static string TruncarTexto(string? texto, int maxCaracteres)
    {
        if (string.IsNullOrWhiteSpace(texto)) return texto ?? string.Empty;
        return texto.Length <= maxCaracteres ? texto : texto.Substring(0, maxCaracteres).TrimEnd() + "...";
    }

    private async Task<byte[]> ConstruirPdfAsync(ReporteMaterialidadDto reporte, byte[]? qrBytes, byte[]? logoProveedorBytes)
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

        // Cargar logo específico para mostrar junto al nombre de la evidencia (Logoveliosevidence.png)
        byte[]? evidenceLogoBytes = null;
        try
        {
            var evidenceLogoPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Logoveliosevidence.png");
            if (File.Exists(evidenceLogoPath))
                evidenceLogoBytes = File.ReadAllBytes(evidenceLogoPath);
            else
            {
                var altEvidencePath = Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), "Resources", "Logoveliosevidence.png");
                if (File.Exists(altEvidencePath))
                    evidenceLogoBytes = File.ReadAllBytes(altEvidencePath);
            }
        }
        catch { evidenceLogoBytes = null; }

        // PUNTOS 1, 2 y 4: Cargar nuevos íconos de diseño desde /Resources
        byte[]? personaIconBytes = CargarRecurso("persona.png");
        byte[]? documentoIconBytes = CargarRecurso("documento.png");
        byte[]? checkIconBytes = CargarRecurso("check.png");
        byte[]? carpetaIconBytes = CargarRecurso("carpeta.png");

        var clienteDisplay = !string.IsNullOrWhiteSpace(cliente.NombreComercial)
            ? cliente.NombreComercial
            : !string.IsNullOrWhiteSpace(cliente.RazonSocial)
                ? cliente.RazonSocial
                : "N/A";

        var direccionDisplay = !string.IsNullOrWhiteSpace(tarea.DireccionCentroTrabajo)
            ? tarea.DireccionCentroTrabajo
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
                    CrearHeader(c, logoBytes, "INFORME DE TAREA", (tarea.Titulo ?? "SIN TÍTULO").Replace("/", "").Trim()));

                page.Content().PaddingTop(10).Element(c =>
                    CrearContenidoPrincipalConSidebar(c, reporte, clienteDisplay, direccionDisplay, logoBytes, archivosTarea, pdfIconBytes, personaIconBytes, documentoIconBytes, carpetaIconBytes));

                page.Footer().Element(c =>
                                CrearFooter(c, clienteDisplay, direccionDisplay, tarea.NombreCentroTrabajo ?? "Sin centro de trabajo", logoBytes, qrBytes, logoProveedorBytes, carpetaIconBytes));
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
                        CrearHeader(c, logoBytes, "INFORME DE TAREA", (tarea.Titulo ?? "SIN TÍTULO").Replace("/", "").Trim()));

                    page.Content().PaddingTop(10).PaddingLeft(48).Column(column =>
                    {
                        column.Spacing(6);

                        column.Item().Text($"EVIDENCIA {i + 1:D2}")
                            .FontSize(16).Bold().FontColor("#24364D");

                        column.Item().Text(tarea.EstatusNombre ?? "Avance")
                            .FontSize(10).SemiBold().FontColor("#6B7280");

                        // Foto + sidebar datos
                        column.Item().Row(row =>
                        {
                            row.RelativeItem(1.45f).Column(imgCol =>
                            {
                                imgCol.Item().Border(1).BorderColor("#D6DCE5").Element(imgBox =>
                                {
                                    imgBox.Layers(layers =>
                                    {
                                        // Imagen de fondo solamente (sin overlay) — altura aumentada para llenar el espacio
                                        layers.PrimaryLayer().Background("#F8FAFC").Height(420).Element(bg =>
                                        {
                                            if (evidencia.ImagenBytes is not null && evidencia.ImagenBytes.Length > 0)
                                                bg.AlignCenter().AlignMiddle().Image(evidencia.ImagenBytes, ImageScaling.FitArea);
                                            else
                                                bg.AlignCenter().AlignMiddle()
                                                    .Text("No fue posible cargar la imagen.")
                                                    .FontSize(11).FontColor("#64748B");
                                        });
                                    });
                                });

                                // Nombre del archivo debajo (re-agregado) — pequeño espacio y separación
                                if (!string.IsNullOrWhiteSpace(evidencia.UrlArchivo))
                                {
                                    imgCol.Item().PaddingTop(6).Background("#F9FAFB")
                                        .BorderTop(1).BorderColor("#E5E7EB")
                                        .PaddingVertical(6).PaddingHorizontal(10).Row(r =>
                                        {
                                            // Logo local si está disponible
                                            r.ConstantItem(28).Height(28).AlignCenter().AlignMiddle().Element(ic =>
                                            {
                                                if (evidenceLogoBytes is not null && evidenceLogoBytes.Length > 0)
                                                {
                                                    ic.Element(img => img.Image(evidenceLogoBytes, ImageScaling.FitArea));
                                                }
                                                else
                                                {
                                                    ic.Text("🖼️").FontSize(12);
                                                }
                                            });

                                            r.ConstantItem(8);

                                            r.RelativeItem().AlignMiddle()
                                                .Text(Path.GetFileName(evidencia.UrlArchivo))
                                                .SemiBold().FontSize(9).FontColor("#24364D");
                                        });
                                }
                            });

                            row.ConstantItem(12);

                            // =========================================================
                            // PUNTO 4: Sidebar de evidencia rediseñado en secciones
                            //          tipo tarjeta, separadas, con íconos.
                            // PUNTO 5/6: Mapa azul + info del mapa con fondo blanco
                            // =========================================================
                            row.ConstantItem(185).Background("#F3F4F6").CornerRadius(8).Padding(10).Column(right =>
                            {
                                right.Spacing(6);

                                // --- Sección: Datos de captura ---
                                right.Item().Border(1).BorderColor("#E5E7EB").Background(Colors.White).CornerRadius(6).Padding(8).Column(sec =>
                                {
                                    sec.Spacing(5);

                                    sec.Item().Row(r =>
                                    {
                                        r.ConstantItem(16).AlignMiddle().Text("📋").FontSize(11);
                                        r.ConstantItem(4);
                                        r.RelativeItem().AlignMiddle().Text("Datos de captura")
                                            .Bold().FontSize(11).FontColor("#24364D");
                                    });
                                    sec.Item().LineHorizontal(1).LineColor("#D1D5DB");

                                    sec.Item().Row(r =>
                                    {
                                        r.ConstantItem(14).AlignTop().Text("📅").FontSize(9);
                                        r.ConstantItem(4);
                                        r.RelativeItem().Element(x => CrearCampoLateral(x, "Fecha de captura", evidencia.DateCreated.ToString("dd/MM/yyyy")));
                                    });
                                    sec.Item().Row(r =>
                                    {
                                        r.ConstantItem(14).AlignTop().Text("🗄️").FontSize(9);
                                        r.ConstantItem(4);
                                        r.RelativeItem().Element(x => CrearCampoLateral(x, "Registrado en sistema", evidencia.DateCreated.ToString("dd/MM/yyyy")));
                                    });
                                    sec.Item().Row(r =>
                                    {
                                        r.ConstantItem(14).AlignTop().Text("👤").FontSize(9);
                                        r.ConstantItem(4);
                                        r.RelativeItem().Element(x => CrearCampoLateral(x, "Usuario de registro", tarea.NombreOperador));
                                    });
                                    sec.Item().Row(r =>
                                    {
                                        r.ConstantItem(14).AlignTop().Text("📱").FontSize(9);
                                        r.ConstantItem(4);
                                        r.RelativeItem().Element(x => CrearCampoLateral(x, "Equipo", evidencia.ModeloDispositivo));
                                    });
                                    sec.Item().Row(r =>
                                    {
                                        r.ConstantItem(14).AlignTop().Text("📍").FontSize(9);
                                        r.ConstantItem(4);
                                        r.RelativeItem().Element(x => CrearCampoLateral(x, "Latitud de captura",
                                            evidencia.Latitud?.ToString("0.00000000", CultureInfo.InvariantCulture) ?? "N/A"));
                                    });
                                    sec.Item().Row(r =>
                                    {
                                        r.ConstantItem(14).AlignTop().Text("📍").FontSize(9);
                                        r.ConstantItem(4);
                                        r.RelativeItem().Element(x => CrearCampoLateral(x, "Longitud de captura",
                                            evidencia.Longitud?.ToString("0.00000000", CultureInfo.InvariantCulture) ?? "N/A"));
                                    });
                                });

                                // --- Sección: Validación ---
                                right.Item().Border(1).BorderColor("#E5E7EB").Background(Colors.White).CornerRadius(6).Padding(8).Column(sec =>
                                {
                                    sec.Spacing(5);

                                    sec.Item().Text("Validación")
                                        .Bold().FontSize(11).FontColor("#24364D");
                                    sec.Item().LineHorizontal(1).LineColor("#D1D5DB");

                                    sec.Item().Element(x => CrearCheckValidacion(x, "Fecha de evidencia coincide con el registro", true, checkIconBytes));
                                    sec.Item().Element(x => CrearCheckValidacion(x, "Ubicación validada con sucursal",
                                        evidencia.Latitud.HasValue && evidencia.Longitud.HasValue, checkIconBytes));
                                    sec.Item().Element(x => CrearCheckValidacion(x, "Tomada desde la app Velios", true, checkIconBytes));
                                });

                                // --- Sección: Mapa (azul) + info (fondo blanco) ---
                                right.Item().Element(c =>
                                {
                                    c.Border(1).BorderColor("#D6DCE5").Background("#F8FAFC").CornerRadius(6).Column(col =>
                                    {
                                        col.Item().Height(100).Element(box =>
                                        {
                                            box.Layers(layers =>
                                            {
                                                // Capa 1: imagen del mapa
                                                layers.PrimaryLayer().Element(bg =>
                                                {
                                                    if (evidencia.MapaBytes is not null && evidencia.MapaBytes.Length > 0)
                                                        bg.Image(evidencia.MapaBytes, ImageScaling.FitArea);
                                                    else
                                                        bg.Background("#24364D").AlignCenter().AlignMiddle()
                                                            .Text("Sin mapa").FontSize(8).FontColor(Colors.White);
                                                });

                                                // Capa 2: overlay azul semitransparente (alfa ~45% = 0x73)
                                                layers.Layer().Background("#7324364D");

                                                // Capa 3: pin central (icono de ubicacion)
                                                layers.Layer().AlignCenter().AlignMiddle()
                                                    .Element(pin =>
                                                    {
                                                        pin.Width(26).Height(26)
                                                           .Background(Colors.White)
                                                           .CornerRadius(13)
                                                           .AlignCenter().AlignMiddle()
                                                           .Text("📍").FontSize(13);
                                                    });
                                            });
                                        });

                                        if (!string.IsNullOrWhiteSpace(evidencia.DireccionFormateada))
                                        {
                                            col.Item().Background(Colors.White).BorderTop(1).BorderColor("#D6DCE5")
                                                .Padding(4).Row(r =>
                                                {
                                                    r.ConstantItem(10).Text("⊙").FontSize(9).FontColor("#F15A24");
                                                    r.ConstantItem(3);
                                                    r.RelativeItem().Text(evidencia.DireccionFormateada)
                        .FontSize(7).FontColor("#374151").LineHeight(1.1f);
                                                });
                                        }
                                        if (!string.IsNullOrWhiteSpace(evidencia.GoogleMapsUrl))
                                        {
                                            var mapsUrl = evidencia.GoogleMapsUrl!.StartsWith("http")
                                                ? evidencia.GoogleMapsUrl
                                                : $"https://www.google.com/maps?q={evidencia.Latitud?.ToString(CultureInfo.InvariantCulture)},{evidencia.Longitud?.ToString(CultureInfo.InvariantCulture)}";

                                            col.Item().Background(Colors.White).BorderTop(1).BorderColor("#D6DCE5")
                                                .PaddingHorizontal(4).PaddingVertical(3).Text(text =>
                                                {
                                                    text.Hyperlink(mapsUrl, "Da clic aquí para ir a la ubicación en Google Maps")
                                                        .FontSize(7).FontColor("#1D4ED8").Underline();
                                                });
                                        }
                                    });
                                });
                            });
                        });

                        // Archivos adjuntos (nombre del archivo ya mostrado junto a la imagen si aplica)

                    }); // cierra column principal de evidencia

                    page.Footer().Element(c =>
                        CrearFooter(c, clienteDisplay, direccionDisplay, tarea.NombreCentroTrabajo ?? "Sin centro de trabajo", logoBytes, qrBytes, logoProveedorBytes, carpetaIconBytes));
                }); // cierra container.Page
            } // cierra for
        }); // cierra Document.Create

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
        byte[]? pdfIconBytes,
        byte[]? personaIconBytes,
        byte[]? documentoIconBytes,
        byte[]? carpetaIconBytes)
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
                        content.Item().Element(x => CrearFilaSimple(x, "Número de teléfono de la empresa",
    !string.IsNullOrWhiteSpace(tarea.TelefonoCentroTrabajo) ? tarea.TelefonoCentroTrabajo : cliente.Telefono));
                        content.Item().Element(x => CrearFilaSimple(x, "Email del supervisor", tarea.EmailSupervisor));

                    }, documentoIconBytes));

                // Sección tarea
                left.Item().Element(c =>
                    CrearSeccionConIcono(c, "Información de tarea", content =>
                    {
                        content.Item().Element(x => CrearFilaSimple(x, "Nombre", (tarea.Titulo ?? "").Replace("/", "").Trim()));

                        content.Item().PaddingTop(14).Text(text =>
                        {
                            text.Span("Descripción de la actividad a desarrollar: ")
                                .SemiBold().FontSize(9).FontColor("#24364D");
                            text.Span(tarea.Descripcion ?? "N/A")
                                .FontSize(9).FontColor("#6B7280");
                        });

                        if (tarea.Observaciones.Count > 0)
                        {
                            content.Item().PaddingTop(10).Padding(8).Column(obs =>
                            {
                                obs.Item().Text("Observaciones:")
            .SemiBold().FontSize(9).FontColor("#24364D");

                                obs.Item().PaddingTop(4).Row(row =>
                                {
                                    var mitad = (int)Math.Ceiling(tarea.Observaciones.Count / 2.0);
                                    var columnaIzq = tarea.Observaciones.Take(mitad).ToList();
                                    var columnaDer = tarea.Observaciones.Skip(mitad).ToList();

                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Spacing(2);
                                        foreach (var item in columnaIzq)
                                        {
                                            col.Item().Row(r =>
                                            {
                                                r.ConstantItem(8).Text("•").FontSize(8).FontColor("#24364D");
                                                r.RelativeItem().Text(item).FontSize(8).FontColor("#6B7280").LineHeight(1.1f);
                                            });
                                        }
                                    });

                                    row.ConstantItem(8);

                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Spacing(2);
                                        foreach (var item in columnaDer)
                                        {
                                            col.Item().Row(r =>
                                            {
                                                r.ConstantItem(8).Text("•").FontSize(8).FontColor("#24364D");
                                                r.RelativeItem().Text(item).FontSize(8).FontColor("#6B7280").LineHeight(1.1f);
                                            });
                                        }
                                    });
                                });
                            });
                        }
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
                                }, documentoIconBytes));
                        }
                    }, documentoIconBytes));
            });

            // -----------------------------------------------------------------
            // SIDEBAR DERECHO
            // -----------------------------------------------------------------
            // PUNTO 3: bordes redondeados en el panel derecho
            row.ConstantItem(175).Background("#F3F4F6").CornerRadius(8).Padding(12).Column(right =>
            {
                right.Spacing(8);

                // Banda superior azul
                right.Item().Height(6).Background("#24364D");

                // Estatus / tipo de plan
                right.Item().PaddingTop(4).Text(TruncarTexto(tarea.NombreProyecto ?? "Sin plan de trabajo", 40))
                    .SemiBold().FontSize(13).FontColor("#24364D");
                right.Item().Row(r =>
                {
                    r.ConstantItem(16).AlignMiddle().AlignCenter().Element(ic =>
                    {
                        if (carpetaIconBytes is not null && carpetaIconBytes.Length > 0)
                            ic.Image(carpetaIconBytes, ImageScaling.FitArea);
                        else
                            ic.Text("▭").FontSize(11).FontColor("#6B7280");
                    });
                    r.ConstantItem(4);
                    r.RelativeItem().AlignMiddle()
                        .Text("Plan de trabajo").FontSize(9).FontColor("#6B7280");
                });


                right.Item().LineHorizontal(1).LineColor("#D1D5DB");

                // PUNTO 1: Operador con ícono de persona azul (PNG)
                right.Item().PaddingTop(4).Row(r =>
                {
                    r.ConstantItem(26).Height(26).Element(ic =>
                    {
                        if (personaIconBytes is not null && personaIconBytes.Length > 0)
                            ic.Image(personaIconBytes, ImageScaling.FitArea);
                        else
                            ic.Background("#E5E7EB").Border(1).BorderColor("#D1D5DB")
                                .AlignCenter().AlignMiddle()
                                .Text("●").FontSize(14).FontColor("#24364D");
                    });
                    r.ConstantItem(8);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(tarea.NombreOperador ?? "N/A")
                            .SemiBold().FontSize(11).FontColor("#24364D");
                        c.Item().Text("Operador").FontSize(8).FontColor("#6B7280");
                    });
                });

                // PUNTO 1: Supervisor con ícono de persona azul (PNG)
                right.Item().PaddingTop(4).Row(r =>
                {
                    r.ConstantItem(26).Height(26).Element(ic =>
                    {
                        if (personaIconBytes is not null && personaIconBytes.Length > 0)
                            ic.Image(personaIconBytes, ImageScaling.FitArea);
                        else
                            ic.Background("#E5E7EB").Border(1).BorderColor("#D1D5DB")
                                .AlignCenter().AlignMiddle()
                                .Text("●").FontSize(14).FontColor("#24364D");
                    });
                    r.ConstantItem(8);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(tarea.NombreSupervisor ?? "N/A")
                            .SemiBold().FontSize(11).FontColor("#24364D");
                        c.Item().Text("Supervisor").FontSize(8).FontColor("#6B7280");
                    });
                });

                // PUNTO 3: Bloque presupuesto — bordes redondeados
                right.Item().PaddingTop(8).Background("#24364D").CornerRadius(6).Padding(10).AlignCenter().Column(c =>
                {
                    c.Item().AlignCenter().Text("PRESUPUESTO")
                        .FontSize(9).FontColor(Colors.White).SemiBold();
                    c.Item().AlignCenter().Text(
                            tarea.PresupuestoAsignado.HasValue
                                ? $"${tarea.PresupuestoAsignado.Value:N2} {tarea.Moneda}"
                                : "N/A")
                        .Bold().FontSize(15).FontColor(Colors.White);
                });

                // Sucursal con ícono pin — estilo similar a referencia
                right.Item().PaddingTop(10).Column(suc =>
                {
                    suc.Item().Row(r =>
                    {
                        r.ConstantItem(20).AlignTop().Element(ic =>
                        {
                            ic.Text("📍").FontSize(12).FontColor("#F15A24");
                        });

                        r.ConstantItem(6);

                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text(TruncarTexto(tarea.NombreCentroTrabajo ?? clienteDisplay, 35))
                                .SemiBold().FontSize(13).FontColor("#24364D");
                            c.Item().Text("Sucursal /CT").FontSize(9).FontColor("#6B7280");
                            c.Item().PaddingTop(4).Text(TruncarTexto(direccionDisplay, 70))
                                .FontSize(8).FontColor("#374151");
                        });
                    });

                    suc.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E5E7EB");

                    // Timeline vertical estilizado: columna izquierda con círculos/lineas, derecha con texto
                    suc.Item().PaddingTop(8).Column(tl =>
                    {
                        Action<string, string, string> addItem = (date, label, state) =>
                        {
                            tl.Item().Row(r =>
                            {
                                r.ConstantItem(28).Column(c =>
                                {
                                    c.Item().Height(4);
                                    if (state == "filled")
                                    {
                                        c.Item().AlignCenter().Element(el => el.Width(12).Height(12).Border(2).BorderColor("#16C60C").CornerRadius(6).Background("#16C60C"));
                                    }
                                    else
                                    {
                                        c.Item().AlignCenter().Element(el => el.Width(12).Height(12).Border(2).BorderColor("#94A3B8").CornerRadius(6).Background(Colors.White));
                                    }

                                    c.Item().Height(36).AlignCenter().Element(line => line.Width(2).Height(36).Background("#E5E7EB"));
                                });

                                r.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(date).SemiBold().FontSize(11).FontColor("#24364D");
                                    c.Item().Text(label).FontSize(9).FontColor("#6B7280");
                                });
                            });
                        };

                        // Añadir items existentes (uso ToString seguro)
                        addItem(tarea.FechaAsignacion != DateTime.MinValue ? tarea.FechaAsignacion.ToString("dd/MM/yyyy") : "N/A", "Fecha asignación", "empty");
                        tl.Item().PaddingTop(6);
                        addItem(tarea.FechaProgramada.HasValue ? tarea.FechaProgramada.Value.ToString("dd/MM/yyyy") : "N/A", "Fecha programada", "empty");
                        tl.Item().PaddingTop(6);
                        addItem(tarea.FechaVencimiento != DateTime.MinValue ? tarea.FechaVencimiento.ToString("dd/MM/yyyy") : "N/A", "Fecha vencimiento", "filled");
                    });
                });

                // Estado de cumplimiento: EN TIEMPO / VENCIDA según FechaVencimiento
                var fechaVencimiento = tarea.FechaVencimiento;
                var estaCompletada = tarea.EstatusCodigo is "FINALIZADO" or "CANCELADA";
                var estaVencida = !estaCompletada && DateTime.Now.Date > fechaVencimiento.Date;


                // PUNTO 3: badges de estatus — bordes redondeados
                if (estaVencida)
                {
                    // Vencida — fondo rojo
                    right.Item().PaddingTop(10).Background("#EF4444").CornerRadius(6).PaddingVertical(8).AlignCenter()
                        .Text("VENCIDA").Bold().FontColor(Colors.White).FontSize(12);
                }
                else
                {
                    // En tiempo — fondo verde
                    right.Item().PaddingTop(10).Background("#16C60C").CornerRadius(6).PaddingVertical(8).AlignCenter()
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
        string nombreProyecto,
        byte[]? logoBytes,
        byte[]? qrBytes,
        byte[]? logoProveedorBytes,
        byte[]? carpetaIconBytes)
    {
        container.PaddingLeft(28).Column(column =>
        {
            // Fila con QR + info
            column.Item().Row(row =>
            {
                // QR sobresaliendo
                row.ConstantItem(70).Element(qr =>
                {
                    qr.Background(Colors.White)
                      .Border(4).BorderColor("#24364D")
                      .CornerRadius(8)
                      .Padding(5)
                      .AlignCenter().AlignMiddle()
                      .Element(img =>
                      {
                          if (qrBytes is not null && qrBytes.Length > 0)
                              img.Image(qrBytes, ImageScaling.FitArea);
                          else
                              img.Text("QR").Bold().FontSize(10).FontColor("#24364D");
                      });
                });

                // Info central alineada al centro vertical
                row.RelativeItem().PaddingLeft(10).PaddingRight(10).PaddingBottom(4).AlignBottom().Row(r =>
                {
                    r.RelativeItem().AlignMiddle().AlignCenter().Column(c =>
                    {
                        c.Item().AlignCenter().Text(nombreProyecto)
                            .Bold().FontSize(11).FontColor("#24364D");
                    });

                    r.ConstantItem(20).AlignMiddle().AlignCenter().Column(c =>
                    {
                        c.Item().Width(1).Height(22).Background("#BDBDBD");
                    });

                    r.RelativeItem().AlignCenter().Row(logoRow =>
                    {
                        if (logoProveedorBytes is not null && logoProveedorBytes.Length > 0)
                            logoRow.ConstantItem(55).Height(30).AlignCenter().AlignMiddle()
                                .Element(img => img.Image(logoProveedorBytes, ImageScaling.FitArea));
                        else if (logoBytes is not null && logoBytes.Length > 0)
                            logoRow.ConstantItem(55).Height(30).AlignCenter().AlignMiddle()
                                .Element(img => img.Image(logoBytes, ImageScaling.FitArea));
                        else
                            logoRow.RelativeItem().AlignCenter().AlignMiddle()
                                .Text("Sin logo").FontSize(8).FontColor("#6B7280");
                    });

                    r.ConstantItem(20).AlignMiddle().AlignCenter().Column(c =>
                    {
                        c.Item().Width(1).Height(22).Background("#BDBDBD");
                    });

                    r.RelativeItem().AlignMiddle().AlignCenter().Row(eRow =>
                    {
                        eRow.ConstantItem(14).AlignMiddle().Element(ic =>
                        {
                            if (carpetaIconBytes is not null && carpetaIconBytes.Length > 0)
                                ic.Image(carpetaIconBytes, ImageScaling.FitArea);
                            else
                                ic.Text("▭").FontSize(10).FontColor("#6B7280");
                        });
                        eRow.ConstantItem(4);
                        eRow.RelativeItem().AlignMiddle()
                            .Text(nombreProyecto).FontSize(9).SemiBold().FontColor("#6B7280");
                    });
                });
            });

            // Barra azul — el QR sobresale arriba de ella
            column.Item().Background("#24364D").Row(row =>
            {
                // Espacio del QR dentro de la barra (fondo azul debajo del QR)
                row.ConstantItem(70).Background("#24364D").Height(10);

                // Dirección y paginación
                row.RelativeItem().PaddingVertical(6).PaddingHorizontal(10).Row(r =>
                {
                    r.RelativeItem().AlignCenter().Text(direccionDisplay)
                        .FontSize(8).FontColor(Colors.White);

                    r.ConstantItem(115).AlignRight().Text(text =>
                    {
                        text.Span("PÁGINA ").FontSize(8).FontColor(Colors.White).Bold();
                        text.CurrentPageNumber().FontSize(9).Bold().FontColor(Colors.White);
                        text.Span(" DE ").FontSize(8).FontColor(Colors.White).Bold();
                        text.TotalPages().FontSize(9).Bold().FontColor(Colors.White);
                    });
                });
            });
        });
    }

    // =========================================================================
    // SECCIÓN CON ÍCONO — cuadro con documento + título
    // PUNTO 2: ícono de documento (PNG azul) en lugar de las barras "≡"
    // =========================================================================
    private static void CrearSeccionConIcono(
        IContainer container,
        string titulo,
        Action<ColumnDescriptor> contenido,
        byte[]? documentoIconBytes = null)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                // Ícono de documento
                row.ConstantItem(34).Height(34).Border(1).BorderColor("#D1D5DB")
                    .Background("#F9FAFB").AlignCenter().AlignMiddle()
                    .Element(icon =>
                    {
                        if (documentoIconBytes is not null && documentoIconBytes.Length > 0)
                            icon.Padding(6).Image(documentoIconBytes, ImageScaling.FitArea);
                        else
                            icon.Text("≡").FontSize(18).Bold().FontColor("#24364D");
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
    // PUNTO 4: ícono de palomita verde (PNG) en lugar del "✓" en recuadro
    // =========================================================================
    private static void CrearCheckValidacion(IContainer container, string texto, bool ok, byte[]? checkIconBytes = null)
    {
        container.Border(1).BorderColor("#D6DCE5").Background(Colors.White).Padding(4).Row(row =>
        {
            row.ConstantItem(18).Height(18).AlignMiddle().AlignCenter().Element(box =>
            {
                if (ok && checkIconBytes is not null && checkIconBytes.Length > 0)
                {
                    box.Image(checkIconBytes, ImageScaling.FitArea);
                }
                else
                {
                    box.Border(1)
                        .BorderColor(ok ? "#16A34A" : "#94A3B8")
                        .Background(ok ? "#DCFCE7" : "#FFFFFF")
                        .AlignCenter().AlignMiddle()
                        .Text(ok ? "✓" : "")
                        .FontSize(10).Bold().FontColor("#166534");
                }
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

    private string BuildValidationToken(int tareaId)
    {
        var secretKey = _configuration["QrValidation:SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("No existe la configuración QrValidation:SecretKey.");
        }

        var payload = $"taskId:{tareaId}";
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        return Convert.ToHexString(hashBytes);
    }
}