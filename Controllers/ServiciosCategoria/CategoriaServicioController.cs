using Microsoft.AspNetCore.Mvc;
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

        /// <summary>
        /// Obtiene todas las categorías de servicios disponibles.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
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
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
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
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
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

    }

}