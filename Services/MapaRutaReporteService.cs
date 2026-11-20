using SkiaSharp;
using System.Globalization;
using System.Text;

namespace velios.Api.Services;

public class PuntoLeyendaMapaDto
{
    public int Orden { get; set; }
    public DateTime Hora { get; set; }
    public int Cantidad { get; set; }
}

public class MapaRutaReporteDto
{
    public byte[] Imagen { get; set; } = Array.Empty<byte>();
    public bool TieneMapaBase { get; set; }
    public bool Finalizada { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public int TotalEvidencias { get; set; }
    public List<PuntoLeyendaMapaDto> Puntos { get; set; } = new();
    public string? ErrorMapaBase { get; set; }
}

public interface IMapaRutaReporteService
{
    /// <summary>Devuelve null si la tarea no tiene ruta (la página 2 se omite).</summary>
    Task<MapaRutaReporteDto?> GenerarAsync(int tareaId);
}

public class MapaRutaReporteService : IMapaRutaReporteService
{
    private const int Ancho = 448;     // px lógicos (Static Maps acepta hasta 640x640)
    private const int Alto = 640;
    private const int Margen = 40;     // aire alrededor de la ruta
    private const int Escala = 2;      // scale=2 -> imagen de 1280x1280
    private const int MaxConcurrenciaFotos = 6;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly ITareaRutaRepository _rutaRepository;
    private readonly ITareaRutaService _rutaService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public MapaRutaReporteService(
        ITareaRutaRepository rutaRepository,
        ITareaRutaService rutaService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _rutaRepository = rutaRepository;
        _rutaService = rutaService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<MapaRutaReporteDto?> GenerarAsync(int tareaId)
    {
        var ruta = await _rutaRepository.ObtenerUltimaRutaPorTareaAsync(tareaId);
        if (ruta == null) return null;

        var resumen = await _rutaService.ObtenerResumenAsync(ruta.Id);

        var grupos = resumen.GruposEvidencias
            .Where(g => g.Evidencias.Count > 0)
            .OrderBy(g => g.Evidencias.Min(e => e.DateCreated))
            .ToList();

        var esperados = resumen.PuntosEsperados
            .Where(p => p.Latitud.HasValue && p.Longitud.HasValue)
            .OrderBy(p => p.Orden)
            .Select(p => ((double)p.Latitud!.Value, (double)p.Longitud!.Value))
            .ToList();

        var finalizada = ruta.Estado == EstadoRuta.Finalizada
                         && ruta.LatitudFin.HasValue && ruta.LongitudFin.HasValue;

        var inicio = ((double)ruta.LatitudInicio, (double)ruta.LongitudInicio);
        var recorrido = new List<(double Lat, double Lng)> { inicio };
        recorrido.AddRange(grupos.Select(g => ((double)g.LatitudCentro, (double)g.LongitudCentro)));
        (double Lat, double Lng)? fin = null;
        if (finalizada)
        {
            fin = ((double)ruta.LatitudFin!.Value, (double)ruta.LongitudFin!.Value);
            recorrido.Add(fin.Value);
        }

        var todos = recorrido.Concat(esperados).ToList();
        CalcularVista(todos, out var centroLat, out var centroLng, out var zoom);
        var zoomBase = (int)Math.Floor(zoom); // Google solo da zoom entero

        // Mapa base y fotos de los pines en paralelo (solo HTTP, sin BD)
        var baseTask = DescargarMapaBaseAsync(centroLat, centroLng, zoomBase); var fotosTask = DescargarFotosAsync(grupos);
        await Task.WhenAll(baseTask, fotosTask);

        var (baseMap, errorBase) = baseTask.Result;
        var fotos = fotosTask.Result;
        var cantidades = grupos.Select(g => g.Cantidad).ToList();

        byte[] imagen;
        try
        {
            imagen = Dibujar(baseMap, centroLat, centroLng, zoom, zoomBase,
                recorrido, esperados, cantidades, fotos, inicio, fin);
        }
        finally
        {
            baseMap?.Dispose();
            foreach (var f in fotos) f?.Dispose();
        }

        return new MapaRutaReporteDto
        {
            Imagen = imagen,
            TieneMapaBase = baseMap != null,
            ErrorMapaBase = errorBase,
            Finalizada = finalizada,
            FechaInicio = ruta.FechaHoraInicio,
            FechaFin = finalizada ? ruta.FechaHoraFin : null,
            TotalEvidencias = grupos.Sum(g => g.Cantidad),
            Puntos = grupos.Select((g, i) => new PuntoLeyendaMapaDto
            {
                Orden = i + 1,
                Hora = g.Evidencias.Min(e => e.DateCreated),
                Cantidad = g.Cantidad
            }).ToList()
        };
    }

    // ---------- Proyección Web Mercator ----------
    // ---------- Proyección Web Mercator (zoom fraccionario) ----------
    private static double MercX(double lng, double z) => (lng + 180) / 360 * 256 * Math.Pow(2, z);

    private static double MercY(double lat, double z)
    {
        var s = Math.Clamp(Math.Sin(lat * Math.PI / 180), -0.9999, 0.9999);
        return (0.5 - Math.Log((1 + s) / (1 - s)) / (4 * Math.PI)) * 256 * Math.Pow(2, z);
    }

    private static double InvLng(double x, double z) => x / (256 * Math.Pow(2, z)) * 360 - 180;

    private static double InvLat(double y, double z)
    {
        var n = Math.PI - 2 * Math.PI * y / (256 * Math.Pow(2, z));
        return 180 / Math.PI * Math.Atan(Math.Sinh(n));
    }

    private static void CalcularVista(List<(double Lat, double Lng)> pts,
        out double centroLat, out double centroLng, out double zoom)
    {
        var anchoUtil = Ancho - 2 * Margen;
        var altoUtil = Alto - 2 * Margen;

        // Extensión de la ruta a zoom 0; escala linealmente con 2^zoom
        var ancho0 = pts.Max(p => MercX(p.Lng, 0)) - pts.Min(p => MercX(p.Lng, 0));
        var alto0 = pts.Max(p => MercY(p.Lat, 0)) - pts.Min(p => MercY(p.Lat, 0));

        double zFit = 18;
        if (ancho0 > 1e-9) zFit = Math.Min(zFit, Math.Log2(anchoUtil / ancho0));
        if (alto0 > 1e-9) zFit = Math.Min(zFit, Math.Log2(altoUtil / alto0));
        var z = Math.Clamp(zFit, 3, 18);

        var cx = (pts.Min(p => MercX(p.Lng, z)) + pts.Max(p => MercX(p.Lng, z))) / 2;
        var cy = (pts.Min(p => MercY(p.Lat, z)) + pts.Max(p => MercY(p.Lat, z))) / 2;

        centroLat = InvLat(cy, z);
        centroLng = InvLng(cx, z);
        zoom = z;
    }

    // ---------- Mapa base (Google Static Maps) ----------
    private async Task<(SKBitmap? Bmp, string? Error)> DescargarMapaBaseAsync(double lat, double lng, int zoom)
    {
        var apiKey = _configuration["GoogleMaps:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("[MapaRuta] GoogleMaps:ApiKey no está configurada.");
            return (null, "ApiKey no configurada");
        }

        var url =
            "https://maps.googleapis.com/maps/api/staticmap" +
            $"?center={lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}" +
            $"&zoom={zoom}&size={Ancho}x{Alto}&scale={Escala}&maptype=roadmap" +
            "&style=feature:poi|element:labels|visibility:off" +
            $"&key={apiKey}";

        var http = _httpClientFactory.CreateClient();
        string? error = null;

        for (var intento = 1; intento <= 2; intento++)
        {
            using var cts = new CancellationTokenSource(Timeout);
            try
            {
                using var resp = await http.GetAsync(url, cts.Token);
                var bytes = await resp.Content.ReadAsByteArrayAsync(cts.Token);

                if (!resp.IsSuccessStatusCode)
                {
                    var cuerpo = Encoding.UTF8.GetString(bytes.Take(150).ToArray())
                        .Replace("\r", " ").Replace("\n", " ");
                    error = $"HTTP {(int)resp.StatusCode}: {cuerpo}";
                    Console.WriteLine($"[MapaRuta] Static Maps -> {error}");
                    if ((int)resp.StatusCode < 500) return (null, error);
                    continue;
                }

                var bmp = SKBitmap.Decode(bytes);
                if (bmp != null) return (bmp, null);
                error = "Google respondió 200 pero la imagen no se pudo decodificar";
            }
            catch (Exception ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                Console.WriteLine($"[MapaRuta] Intento {intento} falló Static Maps: {error}");
                if (intento < 2) await Task.Delay(400);
            }
        }
        return (null, error);
    }

    // ---------- Fotos de los pines ----------
    private async Task<List<SKBitmap?>> DescargarFotosAsync(List<GrupoEvidenciasDto> grupos)
    {
        var http = _httpClientFactory.CreateClient();
        using var semaforo = new SemaphoreSlim(MaxConcurrenciaFotos);

        async Task<SKBitmap?> Una(GrupoEvidenciasDto g)
        {
            var url = g.Evidencias
                .OrderBy(e => e.DateCreated)
                .Select(e => e.UrlArchivo)
                .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
            if (url == null) return null;

            await semaforo.WaitAsync();
            try
            {
                for (var intento = 1; intento <= 3; intento++)
                {
                    using var cts = new CancellationTokenSource(Timeout);
                    try
                    {
                        var bytes = await http.GetByteArrayAsync(url.Trim(), cts.Token);
                        return DecodificarMiniatura(bytes);
                    }
                    catch (Exception ex)
                    {
                        if (intento == 3)
                        {
                            Console.WriteLine($"[MapaRuta] Foto no disponible {url}: {ex.GetType().Name} - {ex.Message}");
                            return null;
                        }
                        await Task.Delay(300 * intento);
                    }
                }
                return null;
            }
            finally
            {
                semaforo.Release();
            }
        }

        var resultados = await Task.WhenAll(grupos.Select(Una));
        return resultados.ToList();
    }

    private static SKBitmap? DecodificarMiniatura(byte[] bytes)
    {
        using var original = SKBitmap.Decode(bytes);
        if (original == null) return null;

        const int maxLado = 240;
        var mayor = Math.Max(original.Width, original.Height);
        if (mayor <= maxLado) return original.Copy();

        var ratio = (float)maxLado / mayor;
        var info = new SKImageInfo(
            Math.Max(1, (int)(original.Width * ratio)),
            Math.Max(1, (int)(original.Height * ratio)));
        return original.Resize(info, new SKSamplingOptions(SKFilterMode.Linear));
    }

    // ---------- Dibujo con SkiaSharp ----------
    private static byte[] Dibujar(
        SKBitmap? baseMap, double centroLat, double centroLng, double zoom, int zoomBase, List<(double Lat, double Lng)> recorrido,
        List<(double Lat, double Lng)> esperados,
        List<int> cantidades,
        List<SKBitmap?> fotos,
        (double Lat, double Lng) inicio,
        (double Lat, double Lng)? fin)
    {
        var anchoPx = Ancho * Escala;
        var altoPx = Alto * Escala;
        using var bmp = new SKBitmap(anchoPx, altoPx);
        using var canvas = new SKCanvas(bmp);

        if (baseMap != null)
        {
            // El mapa viene en zoom entero; se amplía desde el centro hasta el zoom de ajuste
            var k = (float)Math.Pow(2, zoom - zoomBase);
            canvas.Save();
            canvas.Scale(k, k, anchoPx / 2f, altoPx / 2f);
            using var imgBase = SKImage.FromBitmap(baseMap);
            canvas.DrawImage(imgBase, new SKRect(0, 0, anchoPx, altoPx),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            canvas.Restore();
        }
        else
        {
            canvas.Clear(SKColor.Parse("#EAF2FB"));
            using var grid = new SKPaint { Color = SKColor.Parse("#D6E4F0"), StrokeWidth = 2 };
            for (var x = 0; x <= anchoPx; x += 128) canvas.DrawLine(x, 0, x, altoPx, grid);
            for (var y = 0; y <= altoPx; y += 128) canvas.DrawLine(0, y, anchoPx, y, grid);
        }

        var cxPx = MercX(centroLng, zoom);
        var cyPx = MercY(centroLat, zoom);
        SKPoint Proy((double Lat, double Lng) p) => new(
            (float)((MercX(p.Lng, zoom) - cxPx) * Escala + anchoPx / 2.0),
            (float)((MercY(p.Lat, zoom) - cyPx) * Escala + altoPx / 2.0));

        // Ruta planeada (punteada morada)
        if (esperados.Count >= 2)
        {
            using var path = new SKPath();
            path.MoveTo(Proy(esperados[0]));
            foreach (var p in esperados.Skip(1)) path.LineTo(Proy(p));
            using var dash = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 6,
                Color = SKColor.Parse("#7C3AED"), StrokeCap = SKStrokeCap.Round,
                PathEffect = SKPathEffect.CreateDash(new[] { 18f, 14f }, 0)
            };
            canvas.DrawPath(path, dash);
        }

        // Ruta real (negra con borde blanco)
        if (recorrido.Count >= 2)
        {
            using var path = new SKPath();
            path.MoveTo(Proy(recorrido[0]));
            foreach (var p in recorrido.Skip(1)) path.LineTo(Proy(p));
            using var borde = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 14,
                Color = SKColors.White, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round
            };
            using var linea = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 7,
                Color = SKColor.Parse("#111827"), StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round
            };
            canvas.DrawPath(path, borde);
            canvas.DrawPath(path, linea);
        }

        // Inicio (○) y Fin (■) debajo de los pines de evidencia
        var pi = Proy(inicio);
        using var blanco = new SKPaint { IsAntialias = true, Color = SKColors.White };
        using var anillo = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 8, Color = SKColor.Parse("#111827")
        };
        canvas.DrawCircle(pi, 18, blanco);
        canvas.DrawCircle(pi, 18, anillo);

        if (fin.HasValue)
        {
            var pf = Proy(fin.Value);
            using var negro = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#111827") };
            canvas.DrawRect(SKRect.Create(pf.X - 20, pf.Y - 20, 40, 40), blanco);
            canvas.DrawRect(SKRect.Create(pf.X - 15, pf.Y - 15, 30, 30), negro);
        }

        // Pines de evidencia: foto circular + aro naranja + insignia con la cantidad
        const float radio = 40;
        using var aro = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#FF5A2A") };
        using var gris = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#CBD5E1") };
        using var bordeBadge = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4, Color = SKColors.White
        };
        using var txt = new SKPaint { IsAntialias = true, Color = SKColors.White };
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default, 26);

        for (var i = 0; i < cantidades.Count; i++)
        {
            var pt = Proy(recorrido[i + 1]); // recorrido[0] es el inicio

            canvas.DrawCircle(pt, radio + 7, aro);

            var foto = i < fotos.Count ? fotos[i] : null;
            if (foto != null)
            {
                canvas.Save();
                using var clip = new SKPath();
                clip.AddCircle(pt.X, pt.Y, radio);
                canvas.ClipPath(clip, SKClipOperation.Intersect, true);

                var lado = Math.Min(foto.Width, foto.Height);
                var src = SKRect.Create((foto.Width - lado) / 2f, (foto.Height - lado) / 2f, lado, lado);
                var dst = SKRect.Create(pt.X - radio, pt.Y - radio, radio * 2, radio * 2);
                using var img = SKImage.FromBitmap(foto);
                canvas.DrawImage(img, src, dst, new SKSamplingOptions(SKFilterMode.Linear), null);
                canvas.Restore();
            }
            else
            {
                canvas.DrawCircle(pt, radio, gris);
            }

            // Insignia con la cantidad de evidencias del punto
            var bx = pt.X + radio * 0.8f;
            var by = pt.Y - radio * 0.8f;
            canvas.DrawCircle(bx, by, 21, aro);
            canvas.DrawCircle(bx, by, 21, bordeBadge);
            canvas.DrawText(cantidades[i].ToString(), bx, by + 9, SKTextAlign.Center, font, txt);
        }

        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return data.ToArray();
    }

}
/// <summary>Iconos de línea para el reporte, dibujados con SkiaSharp (PNG transparente, cacheados).</summary>
public static class IconosReporte
{
    private static readonly Dictionary<string, byte[]> _cache = new();
    private static readonly object _lock = new();

    public static byte[] Camara(string hex) => Obtener("cam" + hex, c =>
    {
        using var p = Trazo(hex, 7);
        c.DrawRoundRect(new SKRect(10, 26, 86, 80), 10, 10, p);
        using var tapa = new SKPath();
        tapa.MoveTo(30, 26); tapa.LineTo(37, 14); tapa.LineTo(59, 14); tapa.LineTo(66, 26);
        c.DrawPath(tapa, p);
        c.DrawCircle(48, 53, 14, p);
    });

    public static byte[] Reloj(string hex) => Obtener("rel" + hex, c =>
    {
        using var p = Trazo(hex, 7);
        c.DrawCircle(48, 48, 34, p);
        using var manecillas = new SKPath();
        manecillas.MoveTo(48, 26); manecillas.LineTo(48, 48); manecillas.LineTo(63, 57);
        c.DrawPath(manecillas, p);
    });

    public static byte[] Check(string hex) => Obtener("chk" + hex, c =>
    {
        using var p = Trazo(hex, 12);
        using var path = new SKPath();
        path.MoveTo(24, 50); path.LineTo(42, 68); path.LineTo(74, 32);
        c.DrawPath(path, p);
    });

    public static byte[] Pin(string hex) => Obtener("pin" + hex, c =>
    {
        using var relleno = new SKPaint { IsAntialias = true, Color = SKColor.Parse(hex), Style = SKPaintStyle.Fill };
        c.DrawCircle(48, 36, 26, relleno);
        using var punta = new SKPath();
        punta.MoveTo(25, 52); punta.LineTo(71, 52); punta.LineTo(48, 90); punta.Close();
        c.DrawPath(punta, relleno);
        using var hueco = new SKPaint { IsAntialias = true, Color = SKColors.White, Style = SKPaintStyle.Fill };
        c.DrawCircle(48, 36, 10, hueco);
    });

    private static SKPaint Trazo(string hex, float ancho) => new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = ancho,
        Color = SKColor.Parse(hex),
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };

    private static byte[] Obtener(string clave, Action<SKCanvas> dibujar)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(clave, out var existente)) return existente;

            using var bmp = new SKBitmap(96, 96);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.Transparent);
            dibujar(canvas);

            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            return _cache[clave] = data.ToArray();
        }
    }
}