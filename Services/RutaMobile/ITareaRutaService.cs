using velios.Api.Services;

public interface ITareaRutaService
{
    Task<RutaDto> IniciarRutaAsync(IniciarRutaRequest request);
    Task<RutaDto> FinalizarRutaAsync(int rutaId, FinalizarRutaRequest request);
    Task<ResumenRutaDto> ObtenerResumenAsync(int rutaId);
}

public class TareaRutaService : ITareaRutaService
{
    private const double UmbralAgrupacionMetros = 25;
    private readonly ITareaRutaRepository _repository;
    private readonly IReporteMaterialidadRepository _observacionesRepository; // ya existe, la reutilizamos

    public TareaRutaService(
        ITareaRutaRepository repository,
        IReporteMaterialidadRepository observacionesRepository)
    {
        _repository = repository;
        _observacionesRepository = observacionesRepository;
    }

    public async Task<RutaDto> IniciarRutaAsync(IniciarRutaRequest request)
    {
        var rutaExistente = await _repository.ObtenerRutaActivaAsync(request.TareaId);
        if (rutaExistente != null)
            throw new InvalidOperationException("Ya existe una ruta activa para esta tarea.");

        var rutaId = await _repository.InsertarRutaAsync(request);
        return await _repository.ObtenerRutaPorIdAsync(rutaId)
            ?? throw new InvalidOperationException("No se pudo recuperar la ruta creada.");
    }

    public async Task<RutaDto> FinalizarRutaAsync(int rutaId, FinalizarRutaRequest request)
    {
        await _repository.FinalizarRutaAsync(rutaId, request);
        return await _repository.ObtenerRutaPorIdAsync(rutaId)
            ?? throw new InvalidOperationException("No se pudo recuperar la ruta finalizada.");
    }

    public async Task<ResumenRutaDto> ObtenerResumenAsync(int rutaId)
    {
        var ruta = await _repository.ObtenerRutaPorIdAsync(rutaId)
            ?? throw new KeyNotFoundException("Ruta no encontrada.");

        var evidenciasTask = _repository.ObtenerEvidenciasPorRutaAsync(rutaId);
        var observacionesTask = _observacionesRepository.ObtenerObservacionesPorTareaAsync(ruta.TareaId);

        await Task.WhenAll(evidenciasTask, observacionesTask);

        return new ResumenRutaDto
        {
            Ruta = ruta,
            GruposEvidencias = AgruparPorCercania(evidenciasTask.Result),
            Observaciones = observacionesTask.Result
        };
    }
    private List<GrupoEvidenciasDto> AgruparPorCercania(List<EvidenciaGeoDto> evidencias)
    {
        var grupos = new List<GrupoEvidenciasDto>();
        var asignadas = new HashSet<int>();

        foreach (var evidencia in evidencias)
        {
            if (asignadas.Contains(evidencia.EvidenciaId)) continue;

            var grupo = new GrupoEvidenciasDto();
            grupo.Evidencias.Add(evidencia);
            asignadas.Add(evidencia.EvidenciaId);

            foreach (var candidata in evidencias)
            {
                if (asignadas.Contains(candidata.EvidenciaId)) continue;

                var distancia = DistanciaMetros(
                    evidencia.Latitud, evidencia.Longitud,
                    candidata.Latitud, candidata.Longitud);

                if (distancia <= UmbralAgrupacionMetros)
                {
                    grupo.Evidencias.Add(candidata);
                    asignadas.Add(candidata.EvidenciaId);
                }
            }

            grupo.Cantidad = grupo.Evidencias.Count;
            grupo.LatitudCentro = grupo.Evidencias.Average(e => e.Latitud);
            grupo.LongitudCentro = grupo.Evidencias.Average(e => e.Longitud);
            grupos.Add(grupo);
        }

        return grupos;
    }

    private static double DistanciaMetros(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        const double radioTierra = 6371000;
        var dLat = ToRad((double)(lat2 - lat1));
        var dLon = ToRad((double)(lon2 - lon1));

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad((double)lat1)) * Math.Cos(ToRad((double)lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return radioTierra * c;
    }

    private static double ToRad(double grados) => grados * Math.PI / 180;
}