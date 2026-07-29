using Microsoft.Extensions.Caching.Memory;
using velios.Api.Models.ReporteMaterialidad;

namespace velios.Api.Services;

/// <summary>
/// Guarda el progreso de cada generación de reporte en memoria (IMemoryCache),
/// identificado por un jobId (Guid). Se registra como Singleton en Program.cs.
///
/// Usa expiración deslizante: si nadie consulta el progreso de un job en 20
/// minutos, se libera solo. Esto evita que el diccionario crezca sin límite
/// si el usuario cierra el navegador a medio proceso o nunca descarga el PDF.
/// </summary>
public class ProgresoStore
{
    private readonly IMemoryCache _cache;

    private static readonly MemoryCacheEntryOptions Opciones = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(20)
    };

    public ProgresoStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>Crea un nuevo job y regresa su identificador.</summary>
    public Guid Crear()
    {
        var jobId = Guid.NewGuid();
        _cache.Set(Llave(jobId), new ProgresoReporte(), Opciones);
        return jobId;
    }

    /// <summary>Regresa el progreso actual del job, o null si no existe / ya expiró.</summary>
    public ProgresoReporte? Obtener(Guid jobId)
    {
        return _cache.TryGetValue(Llave(jobId), out ProgresoReporte? progreso) ? progreso : null;
    }

    /// <summary>
    /// Aplica una modificación al progreso del job de forma segura. Si el job
    /// ya no existe (expiró o nunca se creó), no hace nada silenciosamente.
    /// </summary>
    public void Actualizar(Guid jobId, Action<ProgresoReporte> update)
    {
        if (_cache.TryGetValue(Llave(jobId), out ProgresoReporte? progreso) && progreso is not null)
        {
            update(progreso);
            // Se vuelve a insertar para refrescar la expiración deslizante.
            _cache.Set(Llave(jobId), progreso, Opciones);
        }
    }

    private static string Llave(Guid jobId) => $"reporte-progreso:{jobId}";
}