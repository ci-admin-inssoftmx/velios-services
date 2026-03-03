using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Proveedores;

<<<<<<< HEAD
namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado de la gestión de proyectos asignados a proveedores.
///
/// Flujo de negocio:
/// 1) Un proyecto es asignado a un proveedor.
/// 2) El proveedor puede aceptar o rechazar el proyecto.
/// 3) Se pueden consultar los proyectos asignados por proveedor.
///
/// Estados sugeridos (EstatusProyectoProveedorId):
/// 1 = Asignado (Pendiente)
/// 2 = Aceptado
/// 3 = Rechazado
///
/// Soporta soft-delete mediante IsDeleted.
/// </summary>
=======
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
[ApiController]
[Route("api/ProveedorProyecto")]
public class ProveedorProyectoController : ControllerBase
{
    private readonly AppDbContext _db;
<<<<<<< HEAD

    /// <summary>
    /// Constructor con inyección del DbContext.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    public ProveedorProyectoController(AppDbContext db) => _db = db;

    // =========================================================
    // POST api/ProveedorProyecto/Asignar
    // =========================================================

    /// <summary>
    /// Asigna un nuevo proyecto a un proveedor.
    ///
    /// Reglas:
    /// - Crea un registro con estado inicial = 1 (Asignado).
    /// - Registra FechaAsignacion en UTC.
    /// - No valida duplicados (se recomienda agregar validación si aplica).
    /// </summary>
    /// <param name="model">Datos del proyecto a asignar.</param>
    /// <returns>Entidad creada.</returns>
=======
    public ProveedorProyectoController(AppDbContext db) => _db = db;

    // =========================
    // Asignar proyecto
    // =========================
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
    [HttpPost("Asignar")]
    public async Task<IActionResult> Asignar(ProveedorProyectoCreateRequest model)
    {
        var entity = new ProveedorProyecto
        {
            ProveedorId = model.ProveedorId,
            ProyectoNombre = model.ProyectoNombre,
            ClienteNombre = model.ClienteNombre,
            FechaAsignacion = DateTime.UtcNow,
<<<<<<< HEAD
            EstatusProyectoProveedorId = 1 // Asignado
=======
            EstatusProyectoProveedorId = 1
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
        };

        _db.ProveedorProyectos.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(entity);
    }

<<<<<<< HEAD
    // =========================================================
    // POST api/ProveedorProyecto/{id}/Responder
    // =========================================================

    /// <summary>
    /// Permite al proveedor aceptar o rechazar un proyecto asignado.
    ///
    /// Reglas:
    /// - El proyecto debe existir.
    /// - Se actualiza el estado del proyecto.
    /// - Se registra FechaRespuesta en UTC.
    /// - Se pueden guardar observaciones.
    /// </summary>
    /// <param name="id">Identificador del proyecto asignado.</param>
    /// <param name="model">Respuesta del proveedor (Aceptar/Rechazar).</param>
    /// <returns>Mensaje de confirmación.</returns>
=======
    // =========================
    // Aceptar / Rechazar
    // =========================
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
    [HttpPost("{id}/Responder")]
    public async Task<IActionResult> Responder(long id, ProveedorProyectoRespuestaRequest model)
    {
        var proyecto = await _db.ProveedorProyectos.FindAsync(id);

        if (proyecto == null)
            return BadRequest("Proyecto inválido.");

        proyecto.EstatusProyectoProveedorId = model.EstatusProyectoProveedorId;
        proyecto.Observaciones = model.Observaciones;
        proyecto.FechaRespuesta = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok("Respuesta registrada.");
    }

<<<<<<< HEAD
    // =========================================================
    // GET api/ProveedorProyecto/Proveedor/{proveedorId}
    // =========================================================

    /// <summary>
    /// Obtiene todos los proyectos asignados a un proveedor específico.
    ///
    /// Solo devuelve registros no eliminados (IsDeleted = false).
    /// </summary>
    /// <param name="proveedorId">Identificador del proveedor.</param>
    /// <returns>Listado de proyectos asignados.</returns>
=======
    // =========================
    // Listar por proveedor
    // =========================
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
    [HttpGet("Proveedor/{proveedorId}")]
    public async Task<IActionResult> GetByProveedor(int proveedorId)
    {
        var data = await _db.ProveedorProyectos
            .Where(x => x.ProveedorId == proveedorId && !x.IsDeleted)
            .ToListAsync();

        return Ok(data);
    }
}