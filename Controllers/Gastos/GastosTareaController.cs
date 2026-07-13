using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Tareas.Requests;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/GastosTarea")]
public class GastosTareaController : ControllerBase
{
    private readonly AppDbContext _db;

    public GastosTareaController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Registra un gasto para una tarea y actualiza el presupuesto disponible y usado.
    /// Todo el proceso se ejecuta dentro de una transacción SQL.
    /// </summary>
    [HttpPost("Guardar")]
    public async Task<ActionResult<ApiResponse<object>>> Guardar([FromBody] GastoTareaRequest model)
    {
        // Validaciones básicas
        if (model.IdTarea <= 0 || model.Gasto <= 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "IdTarea y Gasto son requeridos y deben ser mayores a 0.",
                statusCode = 400
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 1. Buscar la tarea
            var tarea = await _db.Tareas.FirstOrDefaultAsync(x => x.TareaId == model.IdTarea && !x.IsDeleted);

            if (tarea == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    success = false,
                    message = "Tarea no encontrada.",
                    statusCode = 404
                });
            }

            if (tarea.PresupuestoAsignado == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "La tarea no tiene presupuesto asignado.",
                    statusCode = 400
                });
            }

            // 2. Determinar presupuesto disponible actual
            //    Si ya existe PresupuestoDisponible se toma ese valor,
            //    si no existe se toma el PresupuestoAsignado original
            var presupuestoDisponible = tarea.PresupuestoDisponible ?? tarea.PresupuestoAsignado.Value;

            // 3. Restar el gasto al presupuesto disponible
            presupuestoDisponible = presupuestoDisponible - model.Gasto;

            // 4. Calcular presupuesto usado
            var presupuestoUsado = tarea.PresupuestoAsignado.Value - presupuestoDisponible;

            // 5. Actualizar tb_Tareas
            tarea.PresupuestoDisponible = presupuestoDisponible;
            tarea.PresupuestoUsado = presupuestoUsado;
            tarea.DateModified = DateTime.UtcNow;

            // 6. Insertar el gasto en tb_GastosTarea
            var fechaRegistro = DateTime.Now; // ← NUEVO
            _db.GastosTarea.Add(new Models.Tareas.GastoTarea
            {
                IdTarea = model.IdTarea,
                Gasto = model.Gasto,
                FechaRegistro = fechaRegistro,
                Descripcion = model.Descripcion?.Trim(),      // ← NUEVO
                RegisteredById = model.RegisteredById,           // ← NUEVO
                RegisteredByType = model.RegisteredByType?.Trim()  // ← NUEVO

            });

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = "Gasto registrado correctamente.",
                statusCode = 200,
                data = new
                {
                    tarea.PresupuestoAsignado,
                    PresupuestoDisponible = presupuestoDisponible,
                    PresupuestoUsado = presupuestoUsado,
                    fechaRegistro // ← NUEVO
                }
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "Error al registrar el gasto.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }
}