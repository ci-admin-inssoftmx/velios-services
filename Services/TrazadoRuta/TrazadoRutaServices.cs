using Dapper;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using velios.Api.Services.TrazadoRuta;

// =====================================================================
// 1) Codec de polilíneas (algoritmo estándar de Google, precisión 1e5)
// =====================================================================
public static class PolylineCodec
{
    public static List<(double lat, double lng)> Decode(string encoded)
    {
        var puntos = new List<(double lat, double lng)>();
        int index = 0, acumLat = 0, acumLng = 0;

        while (index < encoded.Length)
        {
            acumLat += DecodeValue(encoded, ref index);
            acumLng += DecodeValue(encoded, ref index);
            puntos.Add((acumLat / 1e5, acumLng / 1e5));
        }

        return puntos;
    }

    private static int DecodeValue(string s, ref int index)
    {
        int result = 0, shift = 0, b;
        do
        {
            b = s[index++] - 63;
            result |= (b & 0x1f) << shift;
            shift += 5;
        } while (b >= 0x20);

        return (result & 1) != 0 ? ~(result >> 1) : (result >> 1);
    }

    public static string Encode(IEnumerable<(double lat, double lng)> puntos)
    {
        var sb = new StringBuilder();
        int prevLat = 0, prevLng = 0;

        foreach (var (lat, lng) in puntos)
        {
            int ilat = (int)Math.Round(lat * 1e5);
            int ilng = (int)Math.Round(lng * 1e5);
            EncodeValue(ilat - prevLat, sb);
            EncodeValue(ilng - prevLng, sb);
            prevLat = ilat;
            prevLng = ilng;
        }

        return sb.ToString();
    }

    private static void EncodeValue(int value, StringBuilder sb)
    {
        int v = value < 0 ? ~(value << 1) : (value << 1);
        while (v >= 0x20)
        {
            sb.Append((char)((0x20 | (v & 0x1f)) + 63));
            v >>= 5;
        }
        sb.Append((char)(v + 63));
    }
}

// =====================================================================
// 2) Cliente de Google Routes API
// =====================================================================
public interface IRutasGoogleService
{
    /// <summary>
    /// Calcula la ruta por calles que pasa por todos los puntos, en orden.
    /// Devuelve null si Google falla o no hay suficientes puntos (el motivo queda en el log).
    /// </summary>
    Task<RutaCalculadaDto?> CalcularRutaAsync(IReadOnlyList<(double lat, double lng)> puntos, CancellationToken ct = default);
}

public class RutasGoogleService : IRutasGoogleService
{
    private const string Endpoint = "https://routes.googleapis.com/directions/v2:computeRoutes";

    // Routes API: origen + hasta 25 puntos intermedios + destino por consulta.
    private const int MaxPuntosPorConsulta = 27;

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly ILogger<RutasGoogleService> _logger;

    public RutasGoogleService(HttpClient httpClient, IConfiguration configuration, ILogger<RutasGoogleService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        // No lanza si falta: así un ambiente sin la key no tira todo el API, solo este cálculo.
        _apiKey = configuration["Google:ApiKey"];
    }

    public async Task<RutaCalculadaDto?> CalcularRutaAsync(IReadOnlyList<(double lat, double lng)> puntos, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("Google:ApiKey no está configurada; no se puede calcular la ruta por calles.");
            return null;
        }

        if (puntos.Count < 2)
            return null;

        var todos = new List<(double lat, double lng)>();
        int distanciaTotal = 0;

        // Parte la ruta en tramos si hay más puntos de los que admite una consulta.
        // Cada tramo comparte su último punto con el primero del siguiente.
        for (int inicio = 0; inicio < puntos.Count - 1; inicio += MaxPuntosPorConsulta - 1)
        {
            int fin = Math.Min(inicio + MaxPuntosPorConsulta - 1, puntos.Count - 1);
            var tramo = puntos.Skip(inicio).Take(fin - inicio + 1).ToList();

            var resultado = await CalcularTramoAsync(tramo, ct);
            if (resultado == null)
                return null;

            var decodificados = PolylineCodec.Decode(resultado.Value.Polilinea);
            if (todos.Count > 0 && decodificados.Count > 0)
                decodificados.RemoveAt(0); // evita duplicar el punto de unión entre tramos

            todos.AddRange(decodificados);
            distanciaTotal += resultado.Value.Distancia;
        }

        return new RutaCalculadaDto
        {
            Polilinea = PolylineCodec.Encode(todos),
            DistanciaMetros = distanciaTotal
        };
    }

    private async Task<(string Polilinea, int Distancia)?> CalcularTramoAsync(List<(double lat, double lng)> tramo, CancellationToken ct)
    {
        try
        {
            static object Waypoint((double lat, double lng) p) =>
                new { location = new { latLng = new { latitude = p.lat, longitude = p.lng } } };

            var body = new Dictionary<string, object>
            {
                ["origin"] = Waypoint(tramo[0]),
                ["destination"] = Waypoint(tramo[^1]),
                ["travelMode"] = "DRIVE"
            };

            if (tramo.Count > 2)
                body["intermediates"] = tramo.Skip(1).Take(tramo.Count - 2).Select(p => Waypoint(p)).ToList();

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Goog-Api-Key", _apiKey);
            request.Headers.Add("X-Goog-FieldMask", "routes.distanceMeters,routes.polyline.encodedPolyline");

            using var response = await _httpClient.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // El cuerpo del error de Google dice si la API no está habilitada, si hay restricciones de key, etc.
                _logger.LogWarning("Routes API respondió {Status}: {Body}", (int)response.StatusCode, json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
            {
                _logger.LogWarning("Routes API no devolvió rutas para el tramo solicitado.");
                return null;
            }

            var ruta = routes[0];
            var polilinea = ruta.GetProperty("polyline").GetProperty("encodedPolyline").GetString();
            var distancia = ruta.TryGetProperty("distanceMeters", out var d) ? d.GetInt32() : 0;

            return string.IsNullOrEmpty(polilinea) ? null : (polilinea, distancia);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error al llamar a Routes API.");
            return null;
        }
    }
}

// =====================================================================
// 3) Caché de polilíneas en BD (tb_TareaRutaTrazadoCache)
// =====================================================================
public interface ITrazadoCacheRepository
{
    Task<TrazadoCacheEntry?> ObtenerAsync(int rutaId, byte tipo);
    Task GuardarAsync(int rutaId, byte tipo, string huella, string polilinea, int? distanciaMetros);
}

public class TrazadoCacheRepository : ITrazadoCacheRepository
{
    private readonly string _connectionString;

    public TrazadoCacheRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("VeliosConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexión VeliosConnection.");
    }

    public async Task<TrazadoCacheEntry?> ObtenerAsync(int rutaId, byte tipo)
    {
        const string sql = @"
            SELECT HuellaPuntos, Polilinea, DistanciaMetros
            FROM dbo.tb_TareaRutaTrazadoCache
            WHERE RutaId = @RutaId AND Tipo = @Tipo";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<TrazadoCacheEntry>(sql, new { RutaId = rutaId, Tipo = tipo });
    }

    public async Task GuardarAsync(int rutaId, byte tipo, string huella, string polilinea, int? distanciaMetros)
    {
        const string sql = @"
            UPDATE dbo.tb_TareaRutaTrazadoCache
            SET HuellaPuntos = @Huella, Polilinea = @Polilinea,
                DistanciaMetros = @Distancia, FechaCreacion = GETDATE()
            WHERE RutaId = @RutaId AND Tipo = @Tipo;

            IF @@ROWCOUNT = 0
                INSERT INTO dbo.tb_TareaRutaTrazadoCache
                    (RutaId, Tipo, HuellaPuntos, Polilinea, DistanciaMetros, FechaCreacion)
                VALUES
                    (@RutaId, @Tipo, @Huella, @Polilinea, @Distancia, GETDATE());";

        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new
        {
            RutaId = rutaId,
            Tipo = tipo,
            Huella = huella,
            Polilinea = polilinea,
            Distancia = distanciaMetros
        });
    }
}

// =====================================================================
// 4) Servicio del trazado: arma los puntos, usa caché y llama a Google
// =====================================================================
public interface ITrazadoRutaService
{
    /// <exception cref="KeyNotFoundException">Si la ruta no existe.</exception>
    Task<TrazadoRutaDto> ObtenerTrazadoAsync(int rutaId, CancellationToken ct = default);
}

public class TrazadoRutaService : ITrazadoRutaService
{
    private const byte TipoRecorrido = 1;
    private const byte TipoEsperada = 2;

    // Puntos consecutivos más cerca que esto no aportan nada al enrutar (y gastan consultas).
    private const double DistanciaMinimaEntrePuntosMetros = 20;

    private readonly ITareaRutaService _rutaService;
    private readonly IRutasGoogleService _google;
    private readonly ITrazadoCacheRepository _cache;
    private readonly ILogger<TrazadoRutaService> _logger;

    public TrazadoRutaService(
        ITareaRutaService rutaService,
        IRutasGoogleService google,
        ITrazadoCacheRepository cache,
        ILogger<TrazadoRutaService> logger)
    {
        _rutaService = rutaService;
        _google = google;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TrazadoRutaDto> ObtenerTrazadoAsync(int rutaId, CancellationToken ct = default)
    {
        // Reutiliza el resumen existente (grupos de evidencias + puntos esperados). Lanza KeyNotFoundException si no existe.
        var resumen = await _rutaService.ObtenerResumenAsync(rutaId);

        var puntosRecorrido = ConstruirRecorrido(resumen);
        var puntosEsperada = ConstruirEsperada(resumen);

        var tareaRecorrido = ObtenerOCalcularAsync(rutaId, TipoRecorrido, puntosRecorrido, ct);
        var tareaEsperada = ObtenerOCalcularAsync(rutaId, TipoEsperada, puntosEsperada, ct);
        await Task.WhenAll(tareaRecorrido, tareaEsperada);

        var recorrido = tareaRecorrido.Result;
        var esperada = tareaEsperada.Result;

        var dto = new TrazadoRutaDto { RutaId = rutaId };

        if (recorrido != null)
        {
            dto.RecorridoPolilinea = recorrido.Polilinea;
            dto.RecorridoDistanciaMetros = recorrido.DistanciaMetros;
        }
        else if (puntosRecorrido.Count < 2)
        {
            dto.Avisos.Add("El recorrido no tiene suficientes puntos distintos para trazarlo por calles.");
        }
        else
        {
            dto.Avisos.Add("No se pudo calcular el recorrido por calles.");
        }

        if (esperada != null)
        {
            dto.EsperadaPolilinea = esperada.Polilinea;
            dto.EsperadaDistanciaMetros = esperada.DistanciaMetros;
        }
        else if (resumen.PuntosEsperados.Any())
        {
            dto.Avisos.Add(puntosEsperada.Count < 2
                ? "La ruta esperada tiene menos de dos paradas con coordenadas válidas."
                : "No se pudo calcular la ruta esperada por calles.");
        }

        return dto;
    }

    private async Task<RutaCalculadaDto?> ObtenerOCalcularAsync(
        int rutaId, byte tipo, List<(double lat, double lng)> puntos, CancellationToken ct)
    {
        if (puntos.Count < 2)
            return null;

        var huella = CalcularHuella(puntos);

        try
        {
            var enCache = await _cache.ObtenerAsync(rutaId, tipo);
            if (enCache != null && enCache.HuellaPuntos == huella)
            {
                return new RutaCalculadaDto
                {
                    Polilinea = enCache.Polilinea,
                    DistanciaMetros = enCache.DistanciaMetros ?? 0
                };
            }
        }
        catch (Exception ex)
        {
            // Un fallo del caché no debe impedir mostrar la ruta.
            _logger.LogWarning(ex, "No se pudo leer el caché de trazado (ruta {RutaId}, tipo {Tipo}).", rutaId, tipo);
        }

        var calculada = await _google.CalcularRutaAsync(puntos, ct);
        if (calculada == null)
            return null;

        try
        {
            await _cache.GuardarAsync(rutaId, tipo, huella, calculada.Polilinea, calculada.DistanciaMetros);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el caché de trazado (ruta {RutaId}, tipo {Tipo}).", rutaId, tipo);
        }

        return calculada;
    }

    // Inicio → centro de cada grupo de evidencias (ya vienen en orden cronológico) → fin.
    private static List<(double lat, double lng)> ConstruirRecorrido(ResumenRutaDto resumen)
    {
        var ruta = resumen.Ruta;
        var puntos = new List<(double lat, double lng)>
        {
            ((double)ruta.LatitudInicio, (double)ruta.LongitudInicio)
        };

        foreach (var g in resumen.GruposEvidencias)
            puntos.Add(((double)g.LatitudCentro, (double)g.LongitudCentro));

        if (ruta.LatitudFin.HasValue && ruta.LongitudFin.HasValue)
            puntos.Add(((double)ruta.LatitudFin.Value, (double)ruta.LongitudFin.Value));

        return Depurar(puntos);
    }

    // Solo paradas con coordenadas; orden por Orden y, a igualdad, por TipoParada (1 inicio, 2 parada, 3 fin).
    private static List<(double lat, double lng)> ConstruirEsperada(ResumenRutaDto resumen)
    {
        var puntos = resumen.PuntosEsperados
            .Where(p => p.Latitud.HasValue && p.Longitud.HasValue
                        && !(p.Latitud.Value == 0 && p.Longitud.Value == 0))
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.TipoParada)
            .Select(p => ((double)p.Latitud!.Value, (double)p.Longitud!.Value))
            .ToList();

        return Depurar(puntos);
    }

    // Descarta (0,0) y puntos consecutivos casi idénticos.
    private static List<(double lat, double lng)> Depurar(List<(double lat, double lng)> puntos)
    {
        var resultado = new List<(double lat, double lng)>();

        foreach (var p in puntos)
        {
            if (p.lat == 0 && p.lng == 0)
                continue;

            if (resultado.Count > 0 && DistanciaMetros(resultado[^1], p) < DistanciaMinimaEntrePuntosMetros)
                continue;

            resultado.Add(p);
        }

        return resultado;
    }

    private static string CalcularHuella(List<(double lat, double lng)> puntos)
    {
        var texto = string.Join("|", puntos.Select(p =>
            $"{p.lat.ToString("F6", CultureInfo.InvariantCulture)},{p.lng.ToString("F6", CultureInfo.InvariantCulture)}"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))); // 64 caracteres hex
    }

    private static double DistanciaMetros((double lat, double lng) a, (double lat, double lng) b)
    {
        const double radioTierra = 6371000;
        double ToRad(double g) => g * Math.PI / 180;

        var dLat = ToRad(b.lat - a.lat);
        var dLon = ToRad(b.lng - a.lng);

        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(a.lat)) * Math.Cos(ToRad(b.lat)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return radioTierra * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }
}