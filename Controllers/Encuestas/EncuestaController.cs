using Microsoft.AspNetCore.Mvc;
using velios.Api.Models.Encuestas;
using velios.Api.Services.Encuestas;

namespace velios.Api.Controllers.Encuestas
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class EncuestaController : ControllerBase
    {
        private readonly IEncuestaService _encuestaService;
        private readonly ILogger<EncuestaController> _logger;

        public EncuestaController(
            IEncuestaService encuestaService,
            ILogger<EncuestaController> logger)
        {
            _encuestaService = encuestaService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene la encuesta para llenar, con llenado previo si ya fue respondida parcialmente.
        /// </summary>
        /// <param name="idServicio">Id del servicio (retorna encuesta Id=1 si no existe una asociada).</param>
        /// <param name="tareaId">Id de la tarea.</param>
        [HttpGet("servicio/{idServicio}/tarea/{tareaId}")]
        public async Task<IActionResult> GetEncuesta(int idServicio, int tareaId)
        {
            try
            {
                var encuesta = await _encuestaService.GetEncuestaAsync(idServicio, tareaId);

                if (encuesta == null)
                    return NotFound($"No se encontró encuesta para el servicio {idServicio}.");

                return Ok(encuesta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener encuesta para el servicio {IdServicio} tarea {TareaId}.", idServicio, tareaId);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }

        /// <summary>
        /// Guarda la respuesta de un usuario a una pregunta de la encuesta.
        /// </summary>
        /// <param name="request">TareaId, EncuestaId, PreguntaId, RespuestaId.</param>
        [HttpPost("respuesta")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GuardarRespuesta([FromBody] GuardarRespuestaRequest request)
        {
            try
            {
                if (request.TareaId <= 0 || request.EncuestaId <= 0 ||
                    request.PreguntaId <= 0 || request.RespuestaId <= 0)
                    return BadRequest("Todos los campos son requeridos y deben ser válidos.");

                var resultado = await _encuestaService.GuardarRespuestaAsync(request);

                if (!resultado)
                    return StatusCode(500, "No se pudo guardar la respuesta.");

                return Ok(new { mensaje = "Respuesta guardada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar respuesta de la encuesta.");
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }

        /// <summary>
        /// Obtiene la encuesta con las respuestas del usuario.
        /// <summary>
        /// Obtiene la encuesta con las respuestas del usuario.
        /// </summary>
        /// <param name="idServicio">Id del servicio (retorna encuesta Id=1 si no existe una asociada).</param>
        /// <param name="tareaId">Id de la tarea.</param>
        [HttpGet("servicio/{idServicio}/tarea/{tareaId}/respondida")]
        [ProducesResponseType(typeof(EncuestaModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetEncuestaRespondida(int idServicio, int tareaId)
        {
            try
            {
                var encuesta = await _encuestaService.GetEncuestaRespondidaAsync(idServicio, tareaId);

                if (encuesta == null)
                    return NotFound($"No se encontró encuesta respondida para el servicio {idServicio}.");

                return Ok(encuesta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener encuesta respondida servicio {IdServicio} tarea {TareaId}.", idServicio, tareaId);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }
    }
}