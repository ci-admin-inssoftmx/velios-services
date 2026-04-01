using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/tareaAsignacion")]
public class TareaAsignacionController : ControllerBase
{
    private readonly AppDbContext _db;
    public TareaAsignacionController(AppDbContext db) => _db = db;

    [HttpPut("{taskCode}")]
    public async Task<ActionResult<ApiResponse<object>>> Asignar(string taskCode, [FromBody] AsignacionTareaRequest model)
    {
        try
        {
            if (model.TrabajadorId == null && model.SupervisorId == null)
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "Debe enviar al menos TrabajadorId o SupervisorId.",
                    statusCode = 400
                });

            var tarea = await _db.Tareas.FirstOrDefaultAsync(x => x.TaskCode == taskCode && !x.IsDeleted);
            if (tarea == null)
                return NotFound(new ApiResponse<object> { success = false, message = "Tarea no encontrada.", statusCode = 404 });

            if (model.TrabajadorId.HasValue)
            {
                var trabajador = await _db.ProveedorTrabajadores.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TrabajadorId == model.TrabajadorId && !x.IsDeleted);

                if (trabajador == null)
                    return BadRequest(new ApiResponse<object> { success = false, message = "Trabajador no encontrado.", statusCode = 400 });

                if (trabajador.TipoDeMiembro != "Operador")
                    return BadRequest(new ApiResponse<object> { success = false, message = "El miembro indicado no es de tipo Operador.", statusCode = 400 });

                tarea.TrabajadorId = model.TrabajadorId;
            }

            if (model.SupervisorId.HasValue)
            {
                var supervisor = await _db.ProveedorTrabajadores.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TrabajadorId == model.SupervisorId && !x.IsDeleted);

                if (supervisor == null)
                    return BadRequest(new ApiResponse<object> { success = false, message = "Supervisor no encontrado.", statusCode = 400 });

                if (supervisor.TipoDeMiembro != "Supervisor")
                    return BadRequest(new ApiResponse<object> { success = false, message = "El miembro indicado no es de tipo Supervisor.", statusCode = 400 });

                tarea.SupervisorId = model.SupervisorId;
            }

            tarea.DateModified = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = "Tarea asignada correctamente.",
                statusCode = 200,
                data = new
                {
                    taskCode = tarea.TaskCode,
                    trabajadorId = tarea.TrabajadorId,
                    supervisorId = tarea.SupervisorId
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "Error al asignar tarea.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }
}

public class AsignacionTareaRequest
{
    public long? TrabajadorId { get; set; }
    public long? SupervisorId { get; set; }
}
