using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/TrabajadorEdicion")]
public class TrabajadorEdicionController : ControllerBase
{
    private readonly AppDbContext _db;
    public TrabajadorEdicionController(AppDbContext db) => _db = db;

    // =========================================================
    // PUT api/EdicionTrabajador/{trabajadorId}
    // =========================================================
    [HttpPut("{trabajadorId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Edicion(long trabajadorId, [FromBody] TrabajadorUpdateRequest model)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<object> { success = false, message = "Solicitud inválida.", statusCode = 400 });

            var entity = await _db.ProveedorTrabajadores
                .FirstOrDefaultAsync(x => x.TrabajadorId == trabajadorId && !x.IsDeleted);

            if (entity == null)
                return BadRequest(new ApiResponse<object> { success = false, message = "Trabajador no encontrado.", statusCode = 400 });

            entity.Nombre = model.Nombre.Trim();
            entity.ApellidoPaterno = model.ApellidoPaterno?.Trim();
            entity.ApellidoMaterno = model.ApellidoMaterno?.Trim();
            entity.CURP = model.CURP?.Trim();
            entity.RFC = model.RFC?.Trim();
            entity.NSS = model.NSS?.Trim();
            entity.Correo = model.Correo?.Trim();
            entity.Telefono = model.Telefono?.Trim();
            entity.TipoDeMiembro = model.TipoDeMiembro?.Trim();
            entity.Nivel = model.Nivel?.Trim();
            entity.Clientes = model.Clientes?.Trim();
            entity.CentroDeTrabajo = model.CentroDeTrabajo?.Trim();
            entity.DateModified = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = "Trabajador actualizado.",
                statusCode = 200,
                data = new { entity.TrabajadorId }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "Error al actualizar trabajador.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }
}