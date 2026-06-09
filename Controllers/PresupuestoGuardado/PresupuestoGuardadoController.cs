using Microsoft.AspNetCore.Mvc;
using velios.Api.Models.PresupuestoGuardado;
using velios.Api.Services.PresupuestoGuardado;

namespace velios.Api.Controllers.PresupuestoGuardado
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class PresupuestoGuardadoController : ControllerBase
    {
        private readonly IPresupuestoGuardadoService _presupuestoGuardadoService;
        private readonly ILogger<PresupuestoGuardadoController> _logger;

        public PresupuestoGuardadoController(
            IPresupuestoGuardadoService presupuestoGuardadoService,
            ILogger<PresupuestoGuardadoController> logger)
        {
            _presupuestoGuardadoService = presupuestoGuardadoService;
            _logger = logger;
        }

        /// <summary>
        /// Guarda un presupuesto para una tarea.
        /// El PresupuestoAsignado se toma automáticamente de tb_Tareas.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GuardarPresupuesto([FromBody] GuardarPresupuestoRequest request)
        {
            try
            {
                if (request.TareaId <= 0)
                    return BadRequest("TareaId es requerido.");

                if (string.IsNullOrWhiteSpace(request.Descripcion))
                    return BadRequest("La descripción es requerida.");

                if (request.Descripcion.Trim().Length > 60)
                    return BadRequest("La descripción no puede exceder 60 caracteres.");

                if (request.PresupuestoDisponible < 0)
                    return BadRequest("El presupuesto disponible no puede ser negativo.");

                var id = await _presupuestoGuardadoService.GuardarPresupuestoAsync(request);
                return Ok(new { presupuestoGuardadoId = id, mensaje = "Presupuesto guardado correctamente." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar presupuesto de la tarea {TareaId}.", request.TareaId);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }

        /// <summary>
        /// Obtiene todos los presupuestos guardados de una tarea.
        /// </summary>
        /// <param name="tareaId">Id de la tarea.</param>
        [HttpGet("{tareaId}")]
        [ProducesResponseType(typeof(IEnumerable<PresupuestoGuardadoModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPresupuestosByTarea(int tareaId)
        {
            try
            {
                var presupuestos = await _presupuestoGuardadoService.GetPresupuestosByTareaAsync(tareaId);

                if (!presupuestos.Any())
                    return NotFound($"No se encontraron presupuestos para la tarea {tareaId}.");

                return Ok(presupuestos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener presupuestos de la tarea {TareaId}.", tareaId);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }
    }
}