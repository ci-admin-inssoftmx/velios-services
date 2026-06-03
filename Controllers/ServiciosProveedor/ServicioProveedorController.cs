using Microsoft.AspNetCore.Mvc;
using velios.Api.Models.ServiciosProveedor;
using velios.Api.Services.ServiciosProveedor;

namespace velios.Api.Controllers.ServiciosProveedor
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ServicioProveedorController : ControllerBase
    {
        private readonly IServicioProveedorService _servicioProveedorService;
        private readonly ILogger<ServicioProveedorController> _logger;

        public ServicioProveedorController(
            IServicioProveedorService servicioProveedorService,
            ILogger<ServicioProveedorController> logger)
        {
            _servicioProveedorService = servicioProveedorService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todos los servicios asignados a un proveedor.
        /// </summary>
        /// <param name="proveedorId">Id del proveedor.</param>
        [HttpGet("{proveedorId}/servicios")]
        [ProducesResponseType(typeof(IEnumerable<ServicioProveedorModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetServiciosByProveedor(int proveedorId)
        {
            try
            {
                var servicios = await _servicioProveedorService.GetServiciosByProveedorAsync(proveedorId);

                if (!servicios.Any())
                    return NotFound($"No se encontraron servicios asignados al proveedor {proveedorId}.");

                return Ok(servicios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener servicios del proveedor {ProveedorId}.", proveedorId);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }

        /// <summary>
        /// Asigna uno o más servicios a un proveedor.
        /// Los servicios duplicados se informan pero no generan error.
        /// </summary>
        [HttpPost("asignar")]
        [ProducesResponseType(typeof(AsignarServiciosResultado), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AsignarServicios([FromBody] AsignarServiciosProveedorRequest request)
        {
            try
            {
                if (request.ProveedorId <= 0)
                    return BadRequest("ProveedorId es requerido.");

                if (request.ServicioIds == null || !request.ServicioIds.Any())
                    return BadRequest("Debe seleccionar al menos un servicio.");

                var resultado = await _servicioProveedorService.AsignarServiciosAsync(request);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar servicios al proveedor {ProveedorId}.", request.ProveedorId);
                return StatusCode(500, "Ocurrió un error al procesar la solicitud.");
            }
        }
    }
}