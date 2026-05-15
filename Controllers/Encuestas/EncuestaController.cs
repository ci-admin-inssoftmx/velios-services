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
        /// <param name="encuestaId">Id de la encuesta.</param>
        /// <param name="tareaId">Id de la tarea.</param>
        [HttpGet("{encuestaId}/tarea/{tareaId}")]
        [ProducesResponseType(typeof(EncuestaModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetEncuesta(int encuestaId, int tareaId)
        {
            try
            {
                var encuesta = await _encuestaService.GetEncuestaAsync(encuestaId, tareaId);

                if (encuesta == null)
                    return NotFound($"No se encontró la encuesta {encuestaId}.");

                return Ok(encuesta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la encuesta {EncuestaId} para la tarea {TareaId}.", encuestaId, tareaId);
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
        /// </summary>
        /// <param name="encuestaId">Id de la encuesta.</param>
        /// <param name="tareaId">Id de la tarea.</param>
        [HttpGet("{encuestaId}/tarea/{tareaId}/respondida")]
        [ProducesResponseType(typeof(EncuestaModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetEncuestaRespondida(int encuestaId, int tareaId)
        {
            try
            {
                var encuesta = await _encuestaService.GetEncuestaRespondidaAsync(encuestaId, tareaId);

                if (encuesta == null)
                    return NotFound($"No se encontró la encuesta {encuestaId} para la tarea {tareaId}.");

                return Ok(encuesta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener encuesta respondida {EncuestaId} tarea {TareaId}.", encuestaId, tareaId);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }
    }
}