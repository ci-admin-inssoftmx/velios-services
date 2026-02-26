using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProveedoresController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProveedoresController(AppDbContext db)
    {
        _db = db;
    }

    // =========================
    // POST /api/Proveedores
    // Alta pública
    // =========================
    [HttpPost("CreateProveedor")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateProveedor([FromBody] ProveedorCreateRequest model)
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

            var correo = (model.CorreoContacto ?? "").Trim().ToLowerInvariant();
            var rfc = (model.RFC ?? "").Trim();
            var razonSocial = (model.RazonSocial ?? "").Trim();

            if (string.IsNullOrWhiteSpace(correo))
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "El correo de contacto es obligatorio.",
                    statusCode = 400
                });
            }

            // 1) NO cargamos el entity completo (evita "Data is Null..." al materializar)
            var proveedorMin = await _db.Proveedores
                .AsNoTracking()
                .Where(p => p.CorreoContacto.ToLower() == correo && p.IsDeleted != true)
                .Select(p => new
                {
                    p.ProveedorId,
                    p.EstatusProveedorId
                })
                .FirstOrDefaultAsync();

            if (proveedorMin == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Debe solicitar primero el enlace de activación.",
                    statusCode = 400
                });
            }

            // 2) Validar ACTIVADO
            if (proveedorMin.EstatusProveedorId != 1) // 1 = ACTIVO
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Debe activar su cuenta desde el enlace enviado al correo antes de continuar.",
                    statusCode = 400
                });
            }

            // 3) Validar RFC duplicado (si RFC viene)
            if (!string.IsNullOrWhiteSpace(rfc))
            {
                var existeRFC = await _db.Proveedores
                    .AsNoTracking()
                    .AnyAsync(p => p.ProveedorId != proveedorMin.ProveedorId
                                   && p.RFC == rfc
                                   && p.IsDeleted != true);

                if (existeRFC)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        request_id = requestId,
                        success = false,
                        message = "Ya existe un proveedor con ese RFC.",
                        statusCode = 400
                    });
                }
            }

            // 4) UPDATE por SQL (sin materializar entidad)
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE dbo.tb_Proveedores
            SET RFC = {rfc},
                RazonSocial = {razonSocial},
                NombreComercial = {model.NombreComercial},
                TelefonoContacto = {model.TelefonoContacto},
                RepresentanteLegal = {model.RepresentanteLegal},
                DateModified = {DateTime.UtcNow},
                ModifiedBy = 'PUBLIC'
            WHERE ProveedorId = {proveedorMin.ProveedorId};
        ");

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Proveedor actualizado con éxito.",
                data = new { proveedorMin.ProveedorId },
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al registrar proveedor.",
                errors = new List<string> { ex.Message },
                statusCode = 400
            });
        }
    }
}