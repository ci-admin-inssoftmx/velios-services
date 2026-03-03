using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;

namespace velios.Api.Controllers;

<<<<<<< HEAD
/// <summary>
/// Controlador encargado de gestionar la relación entre Proveedores y Tipos de Servicio.
///
/// Funcionalidades:
/// - Asignar un tipo de servicio a un proveedor.
/// - Quitar (soft delete) un tipo de servicio.
/// - Listar los servicios activos de un proveedor.
///
/// Implementa:
/// - Prevención de duplicados.
/// - Soft delete mediante IsDeleted.
/// - Consulta optimizada con Join para obtener datos descriptivos del catálogo.
/// </summary>
=======
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
[ApiController]
[Route("api/ProveedorTipoServicio")]
public class ProveedorTipoServicioController : ControllerBase
{
    private readonly AppDbContext _db;
<<<<<<< HEAD

    /// <summary>
    /// Constructor con inyección del DbContext.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    public ProveedorTipoServicioController(AppDbContext db) => _db = db;

    // =========================================================
    // POST api/ProveedorTipoServicio/Asignar
    // =========================================================

    /// <summary>
    /// Asigna un tipo de servicio a un proveedor.
    ///
    /// Reglas:
    /// - No permite duplicados activos.
    /// - Crea relación con IsDeleted = false.
    /// </summary>
    /// <param name="proveedorId">Identificador del proveedor.</param>
    /// <param name="tipoServicioId">Identificador del tipo de servicio.</param>
    /// <returns>Mensaje de confirmación.</returns>
    [HttpPost("Asignar")]
    public async Task<IActionResult> Asignar(
        [FromQuery] int proveedorId,
        [FromQuery] int tipoServicioId)
=======
    public ProveedorTipoServicioController(AppDbContext db) => _db = db;

    // POST api/ProveedorTipoServicio/Asignar
    [HttpPost("Asignar")]
    public async Task<IActionResult> Asignar([FromQuery] int proveedorId, [FromQuery] int tipoServicioId)
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
    {
        var existe = await _db.ProveedorTipoServicios
            .AnyAsync(x => x.ProveedorId == proveedorId
                        && x.TipoServicioId == tipoServicioId
                        && !x.IsDeleted);

<<<<<<< HEAD
        if (existe)
            return BadRequest("Ya asignado.");
=======
        if (existe) return BadRequest("Ya asignado.");
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59

        _db.ProveedorTipoServicios.Add(new()
        {
            ProveedorId = proveedorId,
            TipoServicioId = tipoServicioId,
            IsDeleted = false
        });

        await _db.SaveChangesAsync();
<<<<<<< HEAD

        return Ok("Servicio asignado.");
    }

    // =========================================================
    // DELETE api/ProveedorTipoServicio/Quitar
    // =========================================================

    /// <summary>
    /// Quita (soft delete) un tipo de servicio asignado a un proveedor.
    ///
    /// Reglas:
    /// - Debe existir la relación activa.
    /// - No elimina físicamente el registro.
    /// - Marca IsDeleted = true.
    /// </summary>
    /// <param name="proveedorId">Identificador del proveedor.</param>
    /// <param name="tipoServicioId">Identificador del tipo de servicio.</param>
    /// <returns>Mensaje de confirmación.</returns>
    [HttpDelete("Quitar")]
    public async Task<IActionResult> Quitar(
        [FromQuery] int proveedorId,
        [FromQuery] int tipoServicioId)
=======
        return Ok("Servicio asignado.");
    }

    // DELETE api/ProveedorTipoServicio/Quitar?proveedorId=1&tipoServicioId=2
    [HttpDelete("Quitar")]
    public async Task<IActionResult> Quitar([FromQuery] int proveedorId, [FromQuery] int tipoServicioId)
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
    {
        var item = await _db.ProveedorTipoServicios
            .FirstOrDefaultAsync(x => x.ProveedorId == proveedorId
                                   && x.TipoServicioId == tipoServicioId
                                   && !x.IsDeleted);

<<<<<<< HEAD
        if (item == null)
            return BadRequest("No existe.");

        item.IsDeleted = true;

        await _db.SaveChangesAsync();

        return Ok("Servicio removido.");
    }

    // =========================================================
    // GET api/ProveedorTipoServicio/Proveedor/{proveedorId}
    // =========================================================

    /// <summary>
    /// Obtiene todos los tipos de servicio activos
    /// asignados a un proveedor específico.
    ///
    /// Devuelve:
    /// - TipoServicioId
    /// - Código
    /// - Nombre
    ///
    /// Solo devuelve registros no eliminados.
    /// </summary>
    /// <param name="proveedorId">Identificador del proveedor.</param>
    /// <returns>Listado de servicios asignados.</returns>
=======
        if (item == null) return BadRequest("No existe.");

        item.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Ok("Servicio removido.");
    }

    // GET api/ProveedorTipoServicio/Proveedor/1
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
    [HttpGet("Proveedor/{proveedorId:int}")]
    public async Task<IActionResult> Get(int proveedorId)
    {
        var data = await _db.ProveedorTipoServicios.AsNoTracking()
            .Where(x => x.ProveedorId == proveedorId && !x.IsDeleted)
            .Join(_db.CatTipoServicio.AsNoTracking(),
                  a => a.TipoServicioId,
                  b => b.TipoServicioId,
<<<<<<< HEAD
                  (a, b) => new
                  {
                      b.TipoServicioId,
                      b.Codigo,
                      b.Nombre
                  })
=======
                  (a, b) => new { b.TipoServicioId, b.Codigo, b.Nombre })
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
            .ToListAsync();

        return Ok(data);
    }
}