using velios.Api.Models.Encuestas;

namespace velios.Api.Services.Encuestas
{
    public interface IEncuestaService
    {
        // Servicio 1: Traer encuesta para llenar (con llenado previo si ya respondió)
        Task<EncuestaModel?> GetEncuestaAsync(int idServicio, int tareaId); // ← cambia encuestaId por idServicio

        // Servicio 2: Guardar respuesta de usuario
        Task<bool> GuardarRespuestaAsync(GuardarRespuestaRequest request);

        // Servicio 3: Traer encuesta respondida
        Task<EncuestaModel?> GetEncuestaRespondidaAsync(int encuestaId, int tareaId);
    }
}