using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using velios.Api.Models.ReporteMaterialidad;

namespace velios.Api.Services;

public class ReporteMaterialidadService : IReporteMaterialidadService
{
    private readonly IReporteMaterialidadRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    private static readonly SemaphoreSlim _httpLimit = new(8);

    private static readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> _bytesCache = new();
    private static readonly ConcurrentDictionary<string, Lazy<Task<GeocodingInfoDto?>>> _geoCache = new();

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

        await ProcesarEvidenciasAsync(evidencias);

        tarea.Evidencias = evidencias;

        var reporte = new ReporteMaterialidadDto
        {
            Cliente = cliente,
            Tarea = tarea,
            FechaGeneracion = DateTime.Now,
            Resumen = ConstruirResumen(tarea)
        };

        var qrBytes = GenerarQrSeguro(tarea, tareaId);

        return await ConstruirPdfAsync(reporte, qrBytes);
    }

    private async Task ProcesarEvidenciasAsync(List<EvidenciaReporteDto> evidencias)
    {
        var tareas = evidencias.Select(ProcesarEvidenciaAsync);
        await Task.WhenAll(tareas);
    }

    private async Task ProcesarEvidenciaAsync(EvidenciaReporteDto evidencia)
    {
        var tareas = new List<Task>();

        if (!string.IsNullOrWhiteSpace(evidencia.UrlArchivo))
            tareas.Add(CargarImagenEvidenciaAsync(evidencia));

        if (evidencia.Latitud.HasValue && evidencia.Longitud.HasValue)
        {
            var latitud = evidencia.Latitud.Value;
            var longitud = evidencia.Longitud.Value;

            var lat = latitud.ToString(CultureInfo.InvariantCulture);
            var lng = longitud.ToString(CultureInfo.InvariantCulture);

            evidencia.GoogleMapsUrl = $"https://www.google.com/maps?q={lat},{lng}";

            tareas.Add(CargarMapaEvidenciaAsync(evidencia, latitud, longitud));

            var necesitaGeocoding =
                string.IsNullOrWhiteSpace(evidencia.DireccionFormateada) &&
                string.IsNullOrWhiteSpace(evidencia.Direccion);

            if (necesitaGeocoding)
                tareas.Add(CargarGeocodingEvidenciaAsync(evidencia, latitud, longitud));
        }

        await Task.WhenAll(tareas);

        if (string.IsNullOrWhiteSpace(evidencia.DireccionFormateada))
            evidencia.DireccionFormateada = evidencia.Direccion;
    }

    private async Task CargarImagenEvidenciaAsync(EvidenciaReporteDto evidencia)
    {
        evidencia.ImagenBytes = await DescargarBytesConCacheAsync(evidencia.UrlArchivo!);
    }

    private async Task CargarMapaEvidenciaAsync(EvidenciaReporteDto evidencia, decimal latitud, decimal longitud)
    {
        evidencia.MapaBytes = await DescargarMapaConCacheAsync(latitud, longitud);
    }

    private async Task CargarGeocodingEvidenciaAsync(EvidenciaReporteDto evidencia, decimal latitud, decimal longitud)
    {
        var geo = await ObtenerGeocodingConCacheAsync(latitud, longitud);

        if (geo is null)
            return;

        evidencia.DireccionFormateada = geo.DireccionFormateada;
        evidencia.Colonia = geo.Colonia;
        evidencia.Municipio = geo.Municipio;
        evidencia.Estado = geo.Estado;
        evidencia.CodigoPostal = geo.CodigoPostal;
        evidencia.Pais = geo.Pais;
    }

    private byte[]? GenerarQrSeguro(TareaReporteDto tarea, int tareaId)
    {
        try
        {
            var token = BuildValidationToken(tarea.TareaId);
            var baseUrlFront = _configuration["AppSettings:BaseUrlFront"];

            if (string.IsNullOrWhiteSpace(baseUrlFront))
                return GenerarQrBytes($"TAREA:{tarea.TareaId}");

            var qrUrl =
                $"{baseUrlFront.TrimEnd('/')}/Documentos/Verificar" +
                $"?taskId={tareaId}" +
                $"&token={Uri.EscapeDataString(token)}";

            return GenerarQrBytes(qrUrl);
        }
        catch
        {
            try
            {
                return GenerarQrBytes($"TAREA:{tarea.TareaId}");
            }
            catch
            {
                return null;
            }
        }
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

    private async Task<byte[]?> DescargarBytesConCacheAsync(string url)
    {
        var lazyTask = _bytesCache.GetOrAdd(
            url,
            key => new Lazy<Task<byte[]?>>(
                () => DescargarBytesAsync(key),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        var result = await lazyTask.Value;

        if (result is null)
            _bytesCache.TryRemove(url, out _);

        return result;
    }

    private async Task<byte[]?> DescargarMapaConCacheAsync(decimal latitud, decimal longitud)
    {
        var apiKey = _configuration["GoogleMaps:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var lat = latitud.ToString(CultureInfo.InvariantCulture);
        var lng = longitud.ToString(CultureInfo.InvariantCulture);

        var mapaUrl =
            $"https://maps.googleapis.com/maps/api/staticmap" +
            $"?center={lat},{lng}" +
            $"&zoom=17" +
            $"&size=600x300" +
            $"&scale=1" +
            $"&maptype=roadmap" +
            $"&markers=color:red%7Clabel:E%7C{lat},{lng}" +
            $"&key={apiKey}";

        return await DescargarBytesConCacheAsync(mapaUrl);
    }

    private async Task<GeocodingInfoDto?> ObtenerGeocodingConCacheAsync(decimal latitud, decimal longitud)
    {
        var apiKey = _configuration["GoogleMaps:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var lat = latitud.ToString(CultureInfo.InvariantCulture);
        var lng = longitud.ToString(CultureInfo.InvariantCulture);

        var cacheKey = $"{lat},{lng}";

        var lazyTask = _geoCache.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<GeocodingInfoDto?>>(
                () => ObtenerGeocodingDesdeGoogleAsync(lat, lng, apiKey),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        var result = await lazyTask.Value;

        if (result is null)
            _geoCache.TryRemove(cacheKey, out _);

        return result;
    }

    private async Task<GeocodingInfoDto?> ObtenerGeocodingDesdeGoogleAsync(string lat, string lng, string apiKey)
    {
        try
        {
            await _httpLimit.WaitAsync();

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(4);

                var url =
                    $"https://maps.googleapis.com/maps/api/geocode/json" +
                    $"?latlng={lat},{lng}&language=es&key={apiKey}";

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("results", out var results) ||
                    results.GetArrayLength() == 0)
                    return null;

                var first = results[0];
                var info = new GeocodingInfoDto();

                if (first.TryGetProperty("formatted_address", out var fa))
                    info.DireccionFormateada = fa.GetString();

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

                        if (typeValues.Contains("administrative_area_level_2") &&
                            string.IsNullOrWhiteSpace(info.Municipio))
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
            finally
            {
                _httpLimit.Release();
            }
        }
        catch
        {
            return null;
        }
    }

    private async Task<byte[]?> DescargarBytesAsync(string url)
    {
        try
        {
            await _httpLimit.WaitAsync();

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(4);

                using var response = await client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead
                );

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadAsByteArrayAsync();
            }
            finally
            {
                _httpLimit.Release();
            }
        }
        catch
        {
            return null;
        }
    }

    private async Task<byte[]?> DescargarImagenAsync(string url)
    {
        return await DescargarBytesConCacheAsync(url);
    }

    private async Task<byte[]?> DescargarMapaAsync(decimal latitud, decimal longitud)
    {
        return await DescargarMapaConCacheAsync(latitud, longitud);
    }

    private async Task<GeocodingInfoDto?> ObtenerGeocodingAsync(decimal latitud, decimal longitud)
    {
        return await ObtenerGeocodingConCacheAsync(latitud, longitud);
    }

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

        var logoBytes = CargarRecursoBytes("logo_velios.png");

        var clienteDisplay = !string.IsNullOrWhiteSpace(cliente.Nombre)
            ? cliente.Nombre
            : !string.IsNullOrWhiteSpace(cliente.RazonSocial)
                ? cliente.RazonSocial
                : "N/A";

        var direccionDisplay = !string.IsNullOrWhiteSpace(cliente.Direccion)
            ? cliente.Direccion
            : "Dirección no disponible";

        var archivosTarea = await PrepararArchivosTareaAsync(tarea.ImageURL);

        var pdfIconBytes =
            CargarRecursoBytes("Icon_PDF.png") ??
            CargarRecursoBytes("Icon_PDF.jpg") ??
            CargarRecursoBytes("Icon_PDF.jpeg") ??
            CargarRecursoBytes("Icon_PDF.bmp") ??
            CargarRecursoBytes("Icon_PDF.gif") ??
            CargarRecursoBytes("Icon_PDF.svg");

        var document = Document.Create(container =>
        {
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
                                                text.Hyperlink(evidencia.UrlArchivo, "Abrir / Descargar");
                                            });
                                        });
                                    });
                                });
                            });
                        }

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

    private async Task<List<ArchivoAdjunto>> PrepararArchivosTareaAsync(string? imageUrl)
    {
        var archivos = new List<ArchivoAdjunto>();

        if (string.IsNullOrWhiteSpace(imageUrl))
            return archivos;

        var urls = imageUrl.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var tareas = urls.Select(async url =>
        {
            var ext = Path.GetExtension(url).ToLowerInvariant();
            var fileName = Path.GetFileName(url);
            byte[]? bytes = null;

            if (EsImagen(ext))
                bytes = await DescargarBytesConCacheAsync(url);

            return new ArchivoAdjunto
            {
                Url = url,
                FileName = fileName,
                Extension = ext,
                Bytes = bytes
            };
        });

        archivos.AddRange(await Task.WhenAll(tareas));
        return archivos;
    }

    private static bool EsImagen(string ext)
    {
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
    }

    private static byte[]? CargarRecursoBytes(string fileName)
    {
        var paths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", fileName),
            Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), "Resources", fileName)
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }

        return null;
    }

    private static void CrearFondoPagina(IContainer container)
    {
        container.Layers(layers =>
        {
            layers.PrimaryLayer();
            layers.Layer().Element(layer =>
            {
                layer.Row(row =>
                {
                    row.ConstantItem(22).Background("#F15A24");
                    row.RelativeItem();
                });
            });
        });
    }

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
                row.ConstantItem(150).AlignMiddle().Element(c =>
                {
                    if (logoBytes != null)
                        c.Image(logoBytes, ImageScaling.FitArea);
                    else
                    {
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

                row.ConstantItem(1).PaddingTop(8).PaddingBottom(8).Background("#D1D5DB");
                row.ConstantItem(12);

                row.RelativeItem().AlignMiddle().Element(containerTitles =>
                {
                    containerTitles.Row(r =>
                    {
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

            column.Item().PaddingTop(4).LineHorizontal(1).LineColor("#E5E7EB");
        });
    }

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
            row.RelativeItem(2.7f).PaddingRight(12).Column(left =>
            {
                left.Spacing(14);
                left.Item().Element(lc => { });

                left.Item().Element(c =>
                    CrearSeccionConIcono(c, "Información general  del cliente", content =>
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

                        if (archivosTarea.Count > 0)
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

            row.ConstantItem(175).Background("#F3F4F6").Padding(12).Column(right =>
            {
                right.Spacing(8);

                right.Item().Height(6).Background("#24364D");

                right.Item().PaddingTop(4).Text(tarea.EstatusNombre ?? "Sin estatus")
                    .SemiBold().FontSize(13).FontColor("#24364D");

                right.Item().Row(r =>
                {
                    r.ConstantItem(16).AlignMiddle().AlignCenter()
                        .Text("▭").FontSize(11).FontColor("#6B7280");
                    r.ConstantItem(4);
                    r.RelativeItem().AlignMiddle()
                        .Text("Plan de trabajo").FontSize(9).FontColor("#6B7280");
                });

                right.Item().LineHorizontal(1).LineColor("#D1D5DB");

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

                CrearFechaSidebar(right, tarea.FechaAsignacion, "Fecha asignación", false);
                CrearFechaSidebar(right, tarea.FechaProgramada, "Fecha programada", false);
                CrearFechaSidebar(right, tarea.FechaVencimiento, "Fecha vencimiento", true);

                var fechaVencimiento = tarea.FechaVencimiento;
                var estaVencida = DateTime.Now.Date > fechaVencimiento.Date;

                right.Item().PaddingTop(10)
                    .Background(estaVencida ? "#EF4444" : "#16C60C")
                    .PaddingVertical(8)
                    .AlignCenter()
                    .Text(estaVencida ? "VENCIDA" : "EN TIEMPO")
                    .Bold()
                    .FontColor(Colors.White)
                    .FontSize(12);
            });
        });
    }

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
                row.ConstantItem(62).Height(62).Border(2).BorderColor("#24364D")
                    .Padding(6).AlignCenter().AlignMiddle().Element(qc =>
                    {
                        if (qrBytes is not null && qrBytes.Length > 0)
                        {
                            qc.Image(qrBytes, ImageScaling.FitArea);
                        }
                        else
                        {
                            qc.Text("QR").Bold().FontSize(10).FontColor("#24364D");
                        }
                    });

                row.RelativeItem().PaddingLeft(10).PaddingRight(10).AlignMiddle().Row(r =>
                {
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

    private static void CrearSeccionConIcono(
        IContainer container,
        string titulo,
        Action<ColumnDescriptor> contenido)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
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
        return png.GetGraphic(20);
    }

    private string BuildValidationToken(int tareaId)
    {
        var secretKey = _configuration["QrValidation:SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("No existe la configuración QrValidation:SecretKey.");

        var payload = $"taskId:{tareaId}";
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        return Convert.ToHexString(hashBytes);
    }
}