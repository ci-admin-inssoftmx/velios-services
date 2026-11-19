
using Microsoft.AspNetCore.Mvc;
using velios.Api.Models.ServiciosCategoria;
using velios.Api.Services.ServiciosCategoria;

namespace velios.Api.Controllers.ServiciosCategoria
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CategoriaServicioController : ControllerBase
    {
        private readonly ICategoriaServicioService _categoriaServicioService;
        private readonly ILogger<CategoriaServicioController> _logger;

        public CategoriaServicioController(
            ICategoriaServicioService categoriaServicioService,
            ILogger<CategoriaServicioController> logger)
        {
            _categoriaServicioService = categoriaServicioService;
            _logger = logger;
        }

        // ============================================================
        // CATÁLOGOS
        // ============================================================

        /// <summary>
        /// Obtiene todas las categorías de servicios disponibles.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CategoriaServicioModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCategorias()
        {
            try
            {
                var categorias = await _categoriaServicioService.GetCategoriasAsync();
                return Ok(categorias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener las categorías de servicios.");
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }

        /// <summary>
        /// Obtiene las subcategorías de una categoría de servicio.
        /// </summary>
        /// <param name="categoriaId">Id de la categoría.</param>
        [HttpGet("{categoriaId}/subcategorias")]
        [ProducesResponseType(typeof(IEnumerable<SubcategoriaServicioModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSubcategorias(int categoriaId)
        {
            try
            {
                var subcategorias = await _categoriaServicioService.GetSubcategoriasByCategoriaAsync(categoriaId);

                if (!subcategorias.Any())
                    return NotFound($"No se encontraron subcategorías para la categoría {categoriaId}.");

                return Ok(subcategorias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategorías de la categoría {CategoriaId}.", categoriaId);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }

        /// <summary>
        /// Obtiene los servicios disponibles de una subcategoría.
        /// </summary>
        /// <param name="subcategoriaId">Id de la subcategoría.</param>
        [HttpGet("{subcategoriaId}/servicios")]
        [ProducesResponseType(typeof(IEnumerable<ServicioModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetServicios(int subcategoriaId)
        {
            try
            {
                var servicios = await _categoriaServicioService.GetServiciosBySubcategoriaAsync(subcategoriaId);

                if (!servicios.Any())
                    return NotFound($"No se encontraron servicios para la subcategoría {subcategoriaId}.");

                return Ok(servicios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener servicios de la subcategoría {SubcategoriaId}.", subcategoriaId);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }

        // ============================================================
        // SOLICITUD
        // ============================================================

        /// <summary>
        /// Guarda la solicitud de servicio seleccionada por el usuario.
        /// </summary>
        [HttpPost("solicitud")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GuardarSolicitud([FromBody] GuardarSolicitudRequest request)
        {
            try
            {
                if (request.TareaId <= 0 || request.ServicioId <= 0)
                    return BadRequest("TareaId y ServicioId son requeridos.");

                var solicitudId = await _categoriaServicioService.GuardarSolicitudAsync(request);
                return Ok(new { solicitudId, mensaje = "Solicitud guardada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar la solicitud de servicio.");
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }

        /// <summary>
        /// Edita el servicio de una solicitud existente.
        /// </summary>
        [HttpPut("solicitud")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EditarSolicitud([FromBody] EditarSolicitudRequest request)
        {
            try
            {
                if (request.TareaId <= 0 || request.ServicioId <= 0)
                    return BadRequest("TareaId y ServicioId son requeridos.");

                // ← NUEVO: valida que la tarea exista
                var tareaExiste = await _categoriaServicioService.ValidarTareaExisteAsync(request.TareaId);
                if (!tareaExiste)
                    return NotFound(new { mensaje = $"No se encontró la tarea con Id {request.TareaId}." });

                var resultado = await _categoriaServicioService.EditarSolicitudAsync(request);

                if (!resultado)
                    return NotFound(new { mensaje = $"No se encontró el servicio con Id {request.ServicioId}." });

                return Ok(new { mensaje = "Solicitud actualizada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar la solicitud de la tarea {TareaId}.", request.TareaId);
                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error al procesar la solicitud.",
                    error = ex.Message,
                    detalle = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Consulta el detalle de una solicitud con categoría, subcategoría y servicio.
        /// </summary>
        /// <param name="solicitudId">Id de la solicitud.</param>
        [HttpGet("solicitud/{tareaId}")]
        [ProducesResponseType(typeof(SolicitudServicioModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSolicitud(int tareaId)
        {
            try
            {
                var solicitud = await _categoriaServicioService.GetSolicitudAsync(tareaId);

                if (solicitud == null)
                    return NotFound($"No se encontró la solicitud para la tarea {tareaId}.");

                return Ok(solicitud);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar la solicitud de la tarea {TareaId}.", tareaId);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }
        // ============================================================
        // BUSCADOR DINAMICO NUEVO MEJORADO CON JERARQUÍA
        // ============================================================

        /// <summary>
        /// Busca coincidencias en los tres niveles y devuelve estructura jerárquica.
        /// </summary>
        /// <param name="busqueda">Texto a buscar (mínimo 3 caracteres).</param>
        [HttpGet("buscar/jerarquia")]
        [ProducesResponseType(typeof(BuscadorJerarquiaResultado), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> BuscarJerarquia([FromQuery] string busqueda)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(busqueda) || busqueda.Trim().Length < 3)
                    return BadRequest("Ingresa al menos 3 caracteres para buscar.");

                var resultado = await _categoriaServicioService.BuscarJerarquiaAsync(busqueda);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar búsqueda jerárquica con término '{Busqueda}'.", busqueda);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }
    }
}