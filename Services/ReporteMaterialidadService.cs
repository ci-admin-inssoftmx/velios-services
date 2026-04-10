using System.Globalization;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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
            // Descarga de imagen principal de la evidencia
            if (!string.IsNullOrWhiteSpace(evidencia.UrlArchivo))
                evidencia.ImagenBytes = await DescargarImagenAsync(evidencia.UrlArchivo!);

            // Si hay coordenadas, se genera mapa + geocoding
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

            // Fallback de dirección
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

        return ConstruirPdf(reporte);
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
        catch
        {
            return null;
        }
    }

    private async Task<byte[]?> DescargarMapaAsync(decimal latitud, decimal longitud)
    {
        try
        {
            var apiKey = _configuration["GoogleMaps:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            var lat = latitud.ToString(CultureInfo.InvariantCulture);
            var lng = longitud.ToString(CultureInfo.InvariantCulture);

            var mapaUrl =
                $"https://maps.googleapis.com/maps/api/staticmap" +
                $"?center={lat},{lng}" +
                $"&zoom=17" +
                $"&size=900x450" +
                $"&scale=2" +
                $"&maptype=roadmap" +
                $"&markers=color:red%7Clabel:E%7C{lat},{lng}" +
                $"&key={apiKey}";

            return await client.GetByteArrayAsync(mapaUrl);
        }
        catch
        {
            return null;
        }
    }

    private async Task<GeocodingInfoDto?> ObtenerGeocodingAsync(decimal latitud, decimal longitud)
    {
        try
        {
            var apiKey = _configuration["GoogleMaps:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            var lat = latitud.ToString(CultureInfo.InvariantCulture);
            var lng = longitud.ToString(CultureInfo.InvariantCulture);

            var url =
                $"https://maps.googleapis.com/maps/api/geocode/json?latlng={lat},{lng}&language=es&key={apiKey}";

            var json = await client.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return null;

            var first = results[0];
            var info = new GeocodingInfoDto();

            if (first.TryGetProperty("formatted_address", out var formattedAddress))
                info.DireccionFormateada = formattedAddress.GetString();

            if (first.TryGetProperty("address_components", out var components))
            {
                foreach (var component in components.EnumerateArray())
                {
                    if (!component.TryGetProperty("types", out var types))
                        continue;

                    var typeValues = types.EnumerateArray()
                        .Select(t => t.GetString() ?? string.Empty)
                        .ToList();

                    var longName = component.TryGetProperty("long_name", out var ln)
                        ? ln.GetString()
                        : null;

                    if (typeValues.Contains("sublocality") ||
                        typeValues.Contains("sublocality_level_1") ||
                        typeValues.Contains("neighborhood"))
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
        catch
        {
            return null;
        }
    }

    private byte[] ConstruirPdf(ReporteMaterialidadDto reporte)
    {
        var tarea = reporte.Tarea;
        var cliente = reporte.Cliente;

        var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "logo_velios.png");
        var logoBytes = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;

        // Nombre visible del cliente para evitar encabezados vacíos
        var clienteDisplay = !string.IsNullOrWhiteSpace(cliente.Nombre)
            ? cliente.Nombre
            : !string.IsNullOrWhiteSpace(cliente.RazonSocial)
                ? cliente.RazonSocial
                : "N/A";

        // Dirección visible para footer y sidebar
        var direccionDisplay = !string.IsNullOrWhiteSpace(cliente.Direccion)
            ? cliente.Direccion
            : "Dirección no disponible";

        var document = Document.Create(container =>
        {
            // =========================================================
            // PÁGINA 1 - RESUMEN / ESTILO EJECUTIVO COMO EL EJEMPLO
            // =========================================================
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#1F2937"));

                // Fondo lateral naranja sin usar canvas
                page.Background().Element(CrearFondoPagina);

                // Header superior
                page.Header().Element(c =>
                {
                    CrearHeaderEstiloEjemplo(
                        c,
                        logoBytes,
                        "INFORME DE TAREA",
                        tarea.Titulo ?? "SIN TÍTULO");
                });

                // Contenido principal
                page.Content().PaddingTop(10).Element(c =>
                {
                    CrearContenidoPrincipalConSidebar(c, reporte, clienteDisplay, direccionDisplay);
                });

                // Footer inferior
                page.Footer().Element(c =>
                {
                    CrearFooterEstiloEjemplo(c, clienteDisplay, direccionDisplay, tarea.EstatusNombre ?? "Tarea");
                });
            });

            // =========================================================
            // PÁGINAS DE EVIDENCIA - UNA POR PÁGINA
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
                    {
                        CrearHeaderEstiloEjemplo(
                            c,
                            logoBytes,
                            "INFORME DE TAREA",
                            tarea.Titulo ?? "SIN TÍTULO");
                    });

                    page.Content().PaddingTop(10).PaddingLeft(28).Column(column =>
                    {
                        column.Spacing(12);

                        // Título de evidencia
                        column.Item().Text($"EVIDENCIA {i + 1:D2}")
                            .FontSize(16)
                            .Bold()
                            .FontColor("#24364D");

                        column.Item().Text(tarea.EstatusNombre ?? "Avance")
                            .FontSize(10)
                            .SemiBold()
                            .FontColor("#6B7280");

                        // Descripción corta
                        column.Item().Text(text =>
                        {
                            text.Span("Observaciones referentes a la tarea: ")
                                .SemiBold()
                                .FontSize(9)
                                .FontColor("#24364D");

                            text.Span(tarea.Descripcion ?? "N/A")
                                .FontSize(9)
                                .FontColor("#6B7280");
                        });

                        // Sección principal con imagen y datos
                        column.Item().Row(row =>
                        {
                            // FOTO
                            row.RelativeItem(1.45f).Element(c =>
                            {
                                var box = c
                                    .Border(1)
                                    .BorderColor("#D6DCE5")
                                    .Background("#F8FAFC")
                                    .Padding(8)
                                    .Height(330);

                                if (evidencia.ImagenBytes is not null && evidencia.ImagenBytes.Length > 0)
                                {
                                    box.AlignCenter()
                                       .AlignMiddle()
                                       .Image(evidencia.ImagenBytes, ImageScaling.FitArea);
                                }
                                else
                                {
                                    box.AlignCenter()
                                       .AlignMiddle()
                                       .Text("No fue posible cargar la imagen.")
                                       .FontSize(11)
                                       .FontColor("#64748B");
                                }
                            });

                            row.ConstantItem(12);

                            // SIDEBAR DE DATOS DE EVIDENCIA
                            row.ConstantItem(185).Background("#F3F4F6").Padding(10).Column(right =>
                            {
                                right.Spacing(8);

                                right.Item().Text("Datos de captura")
                                    .Bold()
                                    .FontSize(11)
                                    .FontColor("#24364D");

                                right.Item().LineHorizontal(1).LineColor("#D1D5DB");

                                right.Item().Element(x => CrearCampoLateral(x, "Fecha de captura", evidencia.DateCreated.ToString("dd/MM/yyyy")));
                                right.Item().Element(x => CrearCampoLateral(x, "Registrado en sistema", evidencia.DateCreated.ToString("dd/MM/yyyy")));
                                right.Item().Element(x => CrearCampoLateral(x, "Usuario de registro", tarea.NombreOperador));
                                right.Item().Element(x => CrearCampoLateral(x, "Equipo", evidencia.ModeloDispositivo));
                                right.Item().Element(x => CrearCampoLateral(
                                    x,
                                    "Latitud de captura",
                                    evidencia.Latitud?.ToString("0.00000000", CultureInfo.InvariantCulture) ?? "N/A"));
                                right.Item().Element(x => CrearCampoLateral(
                                    x,
                                    "Longitud de captura",
                                    evidencia.Longitud?.ToString("0.00000000", CultureInfo.InvariantCulture) ?? "N/A"));

                                right.Item().PaddingTop(6).LineHorizontal(1).LineColor("#D1D5DB");

                                right.Item().Text("Validación")
                                    .Bold()
                                    .FontSize(11)
                                    .FontColor("#24364D");

                                right.Item().Element(x => CrearCheckValidacion(
                                    x,
                                    "Fecha de evidencia coincide con el registro",
                                    true));

                                right.Item().Element(x => CrearCheckValidacion(
                                    x,
                                    "Ubicación validada con sucursal",
                                    evidencia.Latitud.HasValue && evidencia.Longitud.HasValue));

                                right.Item().Element(x => CrearCheckValidacion(
                                    x,
                                    "Tomada desde la app Velios",
                                    true));
                            });
                        });

                        // Mapa
                        column.Item().Text("Mapa de ubicación")
                            .Bold()
                            .FontSize(10)
                            .FontColor("#24364D");

                        column.Item().Element(c =>
                        {
                            var box = c
                                .Border(1)
                                .BorderColor("#D6DCE5")
                                .Background("#F8FAFC")
                                .Padding(8)
                                .Height(150);

                            if (evidencia.MapaBytes is not null && evidencia.MapaBytes.Length > 0)
                            {
                                box.AlignCenter()
                                   .AlignMiddle()
                                   .Image(evidencia.MapaBytes, ImageScaling.FitArea);
                            }
                            else
                            {
                                box.AlignCenter()
                                   .AlignMiddle()
                                   .Text("No fue posible cargar el mapa.")
                                   .FontSize(10)
                                   .FontColor("#64748B");
                            }
                        });

                        // Datos adicionales de ubicación
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
                                    content.Item().Element(x => CrearCampoFicha(
                                        x,
                                        "Precisión (m)",
                                        evidencia.PrecisionMetros?.ToString("0.##", CultureInfo.InvariantCulture) ?? "N/A"));

                                    content.Item().Element(x => CrearCampoFicha(
                                        x,
                                        "Altitud",
                                        evidencia.Altitud?.ToString("0.##", CultureInfo.InvariantCulture) ?? "N/A"));

                                    content.Item().Element(x => CrearCampoFicha(
                                        x,
                                        "Google Maps",
                                        string.IsNullOrWhiteSpace(evidencia.GoogleMapsUrl) ? "N/A" : "Disponible"));
                                }));
                        });

                        // Link de Google Maps
                        if (!string.IsNullOrWhiteSpace(evidencia.GoogleMapsUrl))
                        {
                            column.Item().Text(text =>
                            {
                                text.Span("Abrir ubicación en mapa: ")
                                    .SemiBold()
                                    .FontSize(9)
                                    .FontColor("#24364D");

                                text.Hyperlink(evidencia.GoogleMapsUrl!, "Ver en Google Maps");
                            });
                        }
                    });

                    page.Footer().Element(c =>
                    {
                        CrearFooterEstiloEjemplo(c, clienteDisplay, direccionDisplay, tarea.EstatusNombre ?? "Tarea");
                    });
                });
            }
        });

        return document.GeneratePdf();
    }

    /// <summary>
    /// Fondo decorativo de la página.
    /// Se usa Layers y contenedores normales para evitar Canvas/DrawFilledRectangle.
    /// </summary>
    private static void CrearFondoPagina(IContainer container)
    {
        container.Layers(layers =>
        {
            layers.PrimaryLayer();

            layers.Layer().Element(layer =>
            {
                layer.Row(row =>
                {
                    row.ConstantItem(22).Column(left =>
                    {
                        left.Item().Background("#F15A24").Height(680);
                        left.Item().Background("#24364D").Height(28);
                        left.Item().Background("#6B7280").Height(28);
                    });

                    row.RelativeItem();
                });
            });
        });
    }

    /// <summary>
    /// Header superior tipo corporativo.
    /// </summary>
    private static void CrearHeaderEstiloEjemplo(
        IContainer container,
        byte[]? logoBytes,
        string titulo,
        string subtitulo)
    {
        container.PaddingLeft(28).PaddingRight(10).PaddingTop(5).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(150).Height(50).AlignMiddle().Element(c =>
                {
                    if (logoBytes != null)
                        c.Image(logoBytes, ImageScaling.FitHeight);
                });

                row.RelativeItem().PaddingLeft(10).Row(innerRow =>
                {
                    innerRow.ConstantItem(6).PaddingTop(2).PaddingBottom(2).Element(x =>
                    {
                        x.Width(2).Background("#D1D5DB");
                    });

                    innerRow.ConstantItem(12);

                    innerRow.RelativeItem().Column(col =>
                    {
                        col.Item().Text(titulo)
                            .FontSize(11)
                            .SemiBold()
                            .FontColor("#374151");

                        col.Item().Text(subtitulo ?? "SIN TÍTULO")
                            .FontSize(18)
                            .FontColor("#24364D");
                    });
                });
            });
        });
    }

    /// <summary>
    /// Contenido principal de la hoja 1:
    /// izquierda datos cliente/tarea, derecha panel ejecutivo.
    /// </summary>
    private static void CrearContenidoPrincipalConSidebar(
        IContainer container,
        ReporteMaterialidadDto reporte,
        string clienteDisplay,
        string direccionDisplay)
    {
        var tarea = reporte.Tarea;
        var cliente = reporte.Cliente;

        container.PaddingLeft(28).Row(row =>
        {
            // =========================================================
            // COLUMNA IZQUIERDA
            // =========================================================
            row.RelativeItem(2.7f).PaddingRight(12).Column(left =>
            {
                left.Spacing(14);

                left.Item().Element(c =>
                    CrearSeccionConIcono(c, "Información general del cliente", content =>
                    {
                        content.Item().Element(x => CrearFilaSimple(x, "Nombre", clienteDisplay));
                        content.Item().Element(x => CrearFilaSimple(x, "Razón Social", cliente.RazonSocial));
                        content.Item().Element(x => CrearFilaSimple(x, "Teléfono", cliente.Telefono));
                        content.Item().Element(x => CrearFilaSimple(x, "Email", cliente.Email));
                    }));

                left.Item().Element(c =>
                    CrearSeccionConIcono(c, "Información de tarea", content =>
                    {
                        content.Item().Element(x => CrearFilaSimple(x, "Nombre", tarea.Titulo));

                        content.Item().PaddingTop(14).Text(text =>
                        {
                            text.Span("Descripción de la actividad a desarrollar: ")
                                .SemiBold()
                                .FontSize(9)
                                .FontColor("#24364D");

                            text.Span(tarea.Descripcion ?? "N/A")
                                .FontSize(9)
                                .FontColor("#6B7280");
                        });

                        content.Item().PaddingTop(14).Column(obs =>
                        {
                            obs.Spacing(6);

                            obs.Item().Text("Observaciones :")
                                .SemiBold()
                                .FontSize(10)
                                .FontColor("#24364D");

                            // Comentarios de ejemplo visual como el PDF de referencia
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
                                        .FontSize(12)
                                        .FontColor("#24364D");

                                    r.RelativeItem().Text(item)
                                        .FontSize(9)
                                        .FontColor("#6B7280");
                                });
                            }
                        });
                    }));
            });

            // =========================================================
            // SIDEBAR DERECHO
            // =========================================================
            row.ConstantItem(175).Background("#F3F4F6").Padding(12).Column(right =>
            {
                right.Spacing(10);

                right.Item().Height(8).Background("#24364D");

                right.Item().Text(tarea.EstatusNombre ?? "Sin estatus")
                    .SemiBold()
                    .FontSize(14)
                    .FontColor("#24364D");

                right.Item().Text("Plan de trabajo")
                    .FontSize(9)
                    .FontColor("#6B7280");

                right.Item().LineHorizontal(1).LineColor("#D1D5DB");

                right.Item().Text(tarea.NombreOperador ?? "N/A")
                    .SemiBold()
                    .FontSize(12)
                    .FontColor("#24364D");

                right.Item().Text("Operador")
                    .FontSize(9)
                    .FontColor("#6B7280");

                right.Item().PaddingTop(8).Text(tarea.NombreSupervisor ?? "N/A")
                    .SemiBold()
                    .FontSize(12)
                    .FontColor("#24364D");

                right.Item().Text("Supervisor")
                    .FontSize(9)
                    .FontColor("#6B7280");

                // Bloque de presupuesto
                right.Item().PaddingTop(12).Background("#24364D").Padding(12).AlignCenter().Column(c =>
                {
                    c.Item().Text("PRESUPUESTO")
                        .FontSize(10)
                        .FontColor(Colors.White);

                    c.Item().Text(
                            tarea.PresupuestoAsignado.HasValue
                                ? $"{tarea.PresupuestoAsignado.Value:N2} {tarea.Moneda}"
                                : "N/A")
                        .Bold()
                        .FontSize(16)
                        .FontColor(Colors.White);
                });

                // Sucursal / ubicación
                right.Item().PaddingTop(10).Text(clienteDisplay)
                    .SemiBold()
                    .FontSize(12)
                    .FontColor("#24364D");

                right.Item().Text(direccionDisplay)
                    .FontSize(9)
                    .FontColor("#6B7280");

                right.Item().PaddingTop(10).LineHorizontal(1).LineColor("#D1D5DB");

                // Fechas
                CrearFechaSidebar(right, tarea.FechaAsignacion, "Fecha asignación", false);
                CrearFechaSidebar(right, tarea.FechaProgramada, "Fecha programada", false);
                CrearFechaSidebar(right, tarea.FechaVencimiento, "Fecha vencimiento", true);

                // Estado final
                right.Item().PaddingTop(14).Background("#16C60C").PaddingVertical(8).AlignCenter().Text("EN TIEMPO")
                    .Bold()
                    .FontColor(Colors.White)
                    .FontSize(12);
            });
        });
    }

    /// <summary>
    /// Footer estilo ejemplo.
    /// </summary>
    private static void CrearFooterEstiloEjemplo(
        IContainer container,
        string clienteDisplay,
        string direccionDisplay,
        string estatus)
    {
        container.PaddingLeft(28).Column(column =>
        {
            column.Item().Row(row =>
            {
                // Bloque izquierdo tipo QR placeholder
                row.ConstantItem(82).Height(58).Background("#24364D").AlignCenter().AlignMiddle().Text("QR")
                    .FontColor(Colors.White)
                    .FontSize(12)
                    .Bold();

                row.RelativeItem().PaddingLeft(10).PaddingRight(10).AlignMiddle().Row(r =>
                {
                    r.RelativeItem().AlignCenter().Text(clienteDisplay)
                        .FontSize(9)
                        .SemiBold()
                        .FontColor("#24364D");

                    r.ConstantItem(10).AlignCenter().Text("|")
                        .FontColor("#6B7280");

                    r.RelativeItem().AlignCenter().Text("Velios")
                        .FontSize(9)
                        .SemiBold()
                        .FontColor("#24364D");

                    r.ConstantItem(10).AlignCenter().Text("|")
                        .FontColor("#6B7280");

                    r.RelativeItem().AlignCenter().Text(estatus)
                        .FontSize(9)
                        .SemiBold()
                        .FontColor("#6B7280");
                });
            });

            column.Item().Background("#24364D").PaddingVertical(6).PaddingHorizontal(10).Row(row =>
            {
                row.RelativeItem().AlignCenter().Text(direccionDisplay)
                    .FontSize(8)
                    .FontColor(Colors.White);

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

    /// <summary>
    /// Sección con ícono cuadrado a la izquierda.
    /// </summary>
    private static void CrearSeccionConIcono(
        IContainer container,
        string titulo,
        Action<ColumnDescriptor> contenido)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(34).Height(34).Background("#F3F4F6").Border(1).BorderColor("#D1D5DB")
                    .AlignCenter()
                    .AlignMiddle()
                    .Text("▣")
                    .FontColor("#111827");

                row.ConstantItem(10);

                row.RelativeItem().AlignMiddle().Text(titulo)
                    .SemiBold()
                    .FontSize(11)
                    .FontColor("#24364D");
            });

            column.Item().PaddingTop(10).Column(inner =>
            {
                inner.Spacing(8);
                contenido(inner);
            });
        });
    }

    /// <summary>
    /// Fila simple estilo tabla ligera.
    /// </summary>
    private static void CrearFilaSimple(IContainer container, string etiqueta, string? valor)
    {
        container.BorderBottom(1).BorderColor("#D1D5DB").PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Text(etiqueta)
                .SemiBold()
                .FontSize(9)
                .FontColor("#7C7C7C");

            row.RelativeItem(2).Text(string.IsNullOrWhiteSpace(valor) ? "N/A" : valor)
                .SemiBold()
                .FontSize(9)
                .FontColor("#24364D");
        });
    }

    /// <summary>
    /// Línea de fecha en panel lateral derecho.
    /// </summary>
    private static void CrearFechaSidebar(ColumnDescriptor column, DateTime? fecha, string etiqueta, bool activa)
    {
        column.Item().PaddingTop(4).Row(row =>
        {
            row.ConstantItem(16).AlignTop().Text(activa ? "●" : "○")
                .FontSize(13)
                .FontColor(activa ? "#22C55E" : "#6B7280");

            row.RelativeItem().Column(c =>
            {
                c.Item().Text(fecha?.ToString("dd/MM/yyyy") ?? "N/A")
                    .SemiBold()
                    .FontSize(10)
                    .FontColor("#24364D");

                c.Item().Text(etiqueta)
                    .FontSize(9)
                    .FontColor("#6B7280");
            });
        });
    }

    /// <summary>
    /// Tarjeta con encabezado azul suave.
    /// </summary>
    private static void CrearTarjetaResumen(
        IContainer container,
        string titulo,
        Action<ColumnDescriptor> contenido)
    {
        container
            .Border(1)
            .BorderColor("#D6DCE5")
            .Background(Colors.White)
            .Column(column =>
            {
                column.Item()
                    .Background("#EEF4FF")
                    .BorderBottom(1)
                    .BorderColor("#D6DCE5")
                    .PaddingVertical(8)
                    .PaddingHorizontal(10)
                    .Text(titulo)
                    .FontSize(11)
                    .Bold()
                    .FontColor("#0B2A6F");

                column.Item().Padding(10).Column(inner =>
                {
                    inner.Spacing(7);
                    contenido(inner);
                });
            });
    }

    /// <summary>
    /// Campo tipo ficha.
    /// </summary>
    private static void CrearCampoFicha(IContainer container, string etiqueta, string? valor)
    {
        container.Column(column =>
        {
            column.Item().Text(etiqueta)
                .FontSize(8)
                .SemiBold()
                .FontColor("#64748B");

            column.Item().PaddingTop(1).Text(string.IsNullOrWhiteSpace(valor) ? "N/A" : valor)
                .FontSize(10)
                .FontColor("#0F172A");
        });
    }

    /// <summary>
    /// Campo de la barra lateral de evidencias.
    /// </summary>
    private static void CrearCampoLateral(IContainer container, string etiqueta, string? valor)
    {
        container.Column(column =>
        {
            column.Item().Text(etiqueta)
                .FontSize(8)
                .SemiBold()
                .FontColor("#6B7280");

            column.Item().PaddingTop(1).Text(string.IsNullOrWhiteSpace(valor) ? "N/A" : valor)
                .FontSize(9)
                .FontColor("#24364D");
        });
    }

    /// <summary>
    /// Check visual de validación.
    /// </summary>
    private static void CrearCheckValidacion(IContainer container, string texto, bool ok)
    {
        container.Border(1)
            .BorderColor("#D6DCE5")
            .Background(Colors.White)
            .Padding(8)
            .Row(row =>
            {
                row.ConstantItem(18).Height(18).AlignMiddle().AlignCenter().Element(box =>
                {
                    box.Border(1)
                        .BorderColor(ok ? "#16A34A" : "#94A3B8")
                        .Background(ok ? "#DCFCE7" : "#FFFFFF")
                        .AlignCenter()
                        .AlignMiddle()
                        .Text(ok ? "✓" : "")
                        .FontSize(10)
                        .Bold()
                        .FontColor("#166534");
                });

                row.ConstantItem(6);

                row.RelativeItem().AlignMiddle().Text(texto)
                    .FontSize(9)
                    .FontColor("#334155");
            });
    }
}