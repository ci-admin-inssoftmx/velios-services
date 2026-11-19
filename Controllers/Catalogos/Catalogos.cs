using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;





namespace velios.Api.Controllers
{

    [ApiController]
    [Route("api/catalogos")]
    public class Catalogos : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<TareasController> _logger;

        public Catalogos(AppDbContext db, ILogger<TareasController> logger)
        {
            _db = db;
            _logger = logger;
        }


        /// <summary>
        /// Obtiene el catálogo de estatus de tareas activos, ordenados de forma ascendente por porcentaje de avance.
        /// </summary>
        /// <returns>
        /// Una respuesta HTTP 200 con el catálogo de estatus de tareas en caso de éxito.
        /// En caso de error, devuelve una respuesta HTTP 400 con el mensaje correspondiente.
        /// </returns>
        [HttpGet("CatEstatusTarea")]
        public async Task<ActionResult<ApiResponse<object>>> CatEstatusTarea()
        {

            try
            {
                var catEstatus = await _db.EstatusTareas.AsNoTracking().Where(e => e.Activo == true).OrderBy(g => g.Porcentaje).ToListAsync();


                return Ok(new ApiResponse<object>
                {
                    success = true,
                    message = "Consulta exitosa.",
                    data = new { catEstatusTarea = catEstatus },
                    statusCode = 200
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al extarer el catalogo de estatus tarea.");

                return BadRequest(new
                {
                    success = false,
                    message = "Ocurrió un error al extarer el catalogo de estatus tarea extra" + ex.Message,
                    
                });
            }
        }
    }
}
