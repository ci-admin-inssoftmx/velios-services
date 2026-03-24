using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado de la gestión de trabajadores pertenecientes a un proveedor.
///
/// Funcionalidades:
/// - Alta de trabajador.
/// - Consulta de trabajadores por proveedor.
///
/// Reglas de negocio:
/// - El proveedor debe existir.
/// - No se permiten trabajadores duplicados por CURP dentro del mismo proveedor.
/// - Se implementa soft delete mediante IsDeleted.
/// - Se registra DateCreated en UTC.
/// </summary>
[ApiController]
[Route("api/Trabajadores")]
public class TrabajadoresController : ControllerBase
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Constructor con inyección del DbContext.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    public TrabajadoresController(AppDbContext db) => _db = db;

    // =========================================================
    // POST api/Trabajadores/Alta
    // =========================================================

    /// <summary>
    /// Da de alta un trabajador para un proveedor específico.
    ///
    /// Validaciones:
    /// - El proveedor debe existir.
    /// - No puede existir otro trabajador con la misma CURP para ese proveedor.
    /// - Se crea con EstatusTrabajadorId = 1 (Activo).
    /// </summary>
    /// <param name="model">Datos del trabajador a registrar.</param>
    /// <returns>Identificador del trabajador creado.</returns>
    [HttpPost("Alta")]
    public async Task<ActionResult<ApiResponse<object>>> Alta([FromBody] TrabajadorCreateRequest model)
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

            // Validar proveedor
            var existeProveedor = await _db.Proveedores.AsNoTracking()
                .AnyAsync(x => x.ProveedorId == model.ProveedorId && x.IsDeleted != true);

            if (!existeProveedor)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Proveedor inválido.",
                    statusCode = 400
                });
            }

            // Validar duplicado por CURP
            var curp = (model.CURP ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(curp))
            {
                var yaExiste = await _db.ProveedorTrabajadores.AnyAsync(t =>
                    t.ProveedorId == model.ProveedorId &&
                    t.CURP == curp &&
                    !t.IsDeleted);

                if (yaExiste)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        
                        success = false,
                        message = "Ya existe un trabajador con esa CURP para este proveedor.",
                        statusCode = 400
                    });
                }
            }

            var entity = new ProveedorTrabajador
            {
                ProveedorId = model.ProveedorId,
                Nombre = model.Nombre.Trim(),
                ApellidoPaterno = model.ApellidoPaterno?.Trim(),
                ApellidoMaterno = model.ApellidoMaterno?.Trim(),
                CURP = string.IsNullOrWhiteSpace(curp) ? null : curp,
                RFC = model.RFC?.Trim(),
                NSS = model.NSS?.Trim(),
                Correo = model.Correo?.Trim(),
                Telefono = model.Telefono?.Trim(),
                EstatusTrabajadorId = 1, // Activo
                DateCreated = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.ProveedorTrabajadores.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = "Trabajador dado de alta.",
                statusCode = 200,
                data = new { entity.TrabajadorId }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al dar de alta trabajador.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    // =========================================================
    // GET api/Trabajadores/Proveedor/{proveedorId}
    // =========================================================

    /// <summary>
    /// Obtiene todos los trabajadores activos de un proveedor.
    ///
    /// Devuelve:
    /// - Datos personales básicos.
    /// - Identificadores fiscales y de seguridad social.
    /// - Estatus actual del trabajador.
    ///
    /// Solo devuelve registros con IsDeleted = false.
    /// </summary>
    /// <param name="proveedorId">Identificador del proveedor.</param>
    /// <returns>Listado de trabajadores.</returns>
    [HttpGet("Proveedor/{proveedorId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> GetByProveedor(int proveedorId)
    {
        var requestId = Guid.NewGuid().ToString();

        var items = await _db.ProveedorTrabajadores.AsNoTracking()
            .Where(x => x.ProveedorId == proveedorId && !x.IsDeleted)
            .OrderByDescending(x => x.TrabajadorId)
            .Select(x => new
            {
                x.TrabajadorId,
                x.Nombre,
                x.ApellidoPaterno,
                x.ApellidoMaterno,
                x.CURP,
                x.RFC,
                x.NSS,
                x.Correo,
                x.Telefono,
                x.EstatusTrabajadorId
            })
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