using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado de la gestión de trabajadores asignados a proyectos de proveedores.
///
/// Flujo de negocio:
/// 1) Un proveedor tiene proyectos asignados.
/// 2) Cada proyecto puede tener múltiples trabajadores.
/// 3) Un trabajador solo puede asignarse una vez por proyecto.
/// 4) Se implementa soft-delete mediante IsDeleted.
/// 
/// Validaciones incluidas:
/// - El proyecto debe existir.
/// - El proyecto debe pertenecer al proveedor.
/// - El trabajador debe existir.
/// - El trabajador debe pertenecer al proveedor.
/// - No permite duplicados.
/// </summary>
[ApiController]
[Route("api/ProyectoTrabajadores")]
public class ProyectoTrabajadoresController : ControllerBase
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Constructor con inyección del DbContext.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    public ProyectoTrabajadoresController(AppDbContext db) => _db = db;

    // =========================================================
    // POST api/ProyectoTrabajadores/Asignar
    // =========================================================

    /// <summary>
    /// Asigna un trabajador a un proyecto específico.
    ///
    /// Reglas:
    /// - El proyecto debe existir y pertenecer al proveedor.
    /// - El trabajador debe existir y pertenecer al proveedor.
    /// - No permite duplicados activos.
    /// - Registra FechaAsignacion en UTC.
    /// </summary>
    /// <param name="model">Datos de asignación del trabajador al proyecto.</param>
    /// <returns>Id de la asignación creada.</returns>
    [HttpPost("Asignar")]
    public async Task<ActionResult<ApiResponse<object>>> Asignar([FromBody] ProyectoTrabajadorAssignRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Solicitud inválida.",
                    statusCode = 400
                });
            }

            // Validar proyecto y proveedor
            var proyecto = await _db.ProveedorProyectos.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProveedorProyectoId == model.ProveedorProyectoId && !x.IsDeleted);

            if (proyecto == null || proyecto.ProveedorId != model.ProveedorId)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Proyecto inválido o proveedor no corresponde.",
                    statusCode = 400
                });
            }

            // Validar trabajador y pertenencia al proveedor
            var trabajador = await _db.ProveedorTrabajadores.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TrabajadorId == model.TrabajadorId && !x.IsDeleted);

            if (trabajador == null || trabajador.ProveedorId != model.ProveedorId)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Trabajador inválido o no pertenece al proveedor.",
                    statusCode = 400
                });
            }

            // Validar duplicado
            var yaAsignado = await _db.ProveedorProyectoTrabajadores
                .AnyAsync(x => x.ProveedorProyectoId == model.ProveedorProyectoId
                            && x.TrabajadorId == model.TrabajadorId
                            && !x.IsDeleted);

            if (yaAsignado)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "El trabajador ya está asignado a este proyecto.",
                    statusCode = 400
                });
            }

            var entity = new ProveedorProyectoTrabajador
            {
                ProveedorProyectoId = model.ProveedorProyectoId,
                ProveedorId = model.ProveedorId,
                TrabajadorId = model.TrabajadorId,
                FechaAsignacion = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.ProveedorProyectoTrabajadores.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = "Trabajador asignado al proyecto.",
                statusCode = 200,
                data = new { entity.ProveedorProyectoTrabajadorId }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al asignar trabajador.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    // =========================================================
    // GET api/ProyectoTrabajadores/Proyecto/{proveedorProyectoId}
    // =========================================================

    /// <summary>
    /// Obtiene todos los trabajadores asignados a un proyecto específico.
    ///
    /// Devuelve:
    /// - Información básica del trabajador.
    /// - Datos personales relevantes (CURP, RFC, NSS, etc.).
    /// - Solo registros activos (IsDeleted = false).
    /// </summary>
    /// <param name="proveedorProyectoId">Identificador del proyecto.</param>
    /// <returns>Listado de trabajadores asignados.</returns>
    [HttpGet("Proyecto/{proveedorProyectoId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetByProyecto(long proveedorProyectoId)
    {
        var requestId = Guid.NewGuid().ToString();

        var items = await _db.ProveedorProyectoTrabajadores.AsNoTracking()
            .Where(x => x.ProveedorProyectoId == proveedorProyectoId && !x.IsDeleted)
            .Join(_db.ProveedorTrabajadores.AsNoTracking().Where(t => !t.IsDeleted),
                  a => a.TrabajadorId,
                  t => t.TrabajadorId,
                  (a, t) => new
                  {
                      a.ProveedorProyectoTrabajadorId,
                      a.TrabajadorId,
                      t.Nombre,
                      t.ApellidoPaterno,
                      t.ApellidoMaterno,
                      t.CURP,
                      t.RFC,
                      t.NSS,
                      t.Telefono,
                      t.Correo
                  })
            .OrderByDescending(x => x.TrabajadorId)
            .ToListAsync();

        return Ok(new ApiResponse<object>
        {
            
            success = true,
            message = "Solicitud ejecutada con éxito.",
            statusCode = 200,
            data = new { total = items.Count, items }
        });
    }
}