using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/Trabajadores")]
public class TrabajadoresController : ControllerBase
{
    private readonly AppDbContext _db;
    public TrabajadoresController(AppDbContext db) => _db = db;

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
                    request_id = requestId,
                    success = false,
                    message = "Solicitud inválida.",
                    statusCode = 400
                });
            }

            // proveedor existe
            var existeProveedor = await _db.Proveedores.AsNoTracking()
     .AnyAsync(x => x.ProveedorId == model.ProveedorId && x.IsDeleted != true);

            if (!existeProveedor)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Proveedor inválido.",
                    statusCode = 400
                });
            }

            // anti-duplicado por CURP (si viene)
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
                        request_id = requestId,
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
                EstatusTrabajadorId = 1,
                DateCreated = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.ProveedorTrabajadores.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
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
                request_id = requestId,
                success = false,
                message = "Error al dar de alta trabajador.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

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
            request_id = requestId,
            success = true,
            message = "Solicitud ejecutada con éxito.",
            statusCode = 200,
            data = new { total = items.Count, items }
        });
    }
}