using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/ProveedorTipoServicio")]
public class ProveedorTipoServicioController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProveedorTipoServicioController(AppDbContext db) => _db = db;

    // POST api/ProveedorTipoServicio/Asignar
    [HttpPost("Asignar")]
    public async Task<IActionResult> Asignar([FromQuery] int proveedorId, [FromQuery] int tipoServicioId)
    {
        var existe = await _db.ProveedorTipoServicios
            .AnyAsync(x => x.ProveedorId == proveedorId
                        && x.TipoServicioId == tipoServicioId
                        && !x.IsDeleted);

        if (existe) return BadRequest("Ya asignado.");

        _db.ProveedorTipoServicios.Add(new()
        {
            ProveedorId = proveedorId,
            TipoServicioId = tipoServicioId,
            IsDeleted = false
        });

        await _db.SaveChangesAsync();
        return Ok("Servicio asignado.");
    }

    // DELETE api/ProveedorTipoServicio/Quitar?proveedorId=1&tipoServicioId=2
    [HttpDelete("Quitar")]
    public async Task<IActionResult> Quitar([FromQuery] int proveedorId, [FromQuery] int tipoServicioId)
    {
        var item = await _db.ProveedorTipoServicios
            .FirstOrDefaultAsync(x => x.ProveedorId == proveedorId
                                   && x.TipoServicioId == tipoServicioId
                                   && !x.IsDeleted);

        if (item == null) return BadRequest("No existe.");

        item.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Ok("Servicio removido.");
    }

    // GET api/ProveedorTipoServicio/Proveedor/1
    [HttpGet("Proveedor/{proveedorId:int}")]
    public async Task<IActionResult> Get(int proveedorId)
    {
        var data = await _db.ProveedorTipoServicios.AsNoTracking()
            .Where(x => x.ProveedorId == proveedorId && !x.IsDeleted)
            .Join(_db.CatTipoServicio.AsNoTracking(),
                  a => a.TipoServicioId,
                  b => b.TipoServicioId,
                  (a, b) => new { b.TipoServicioId, b.Codigo, b.Nombre })
            .ToListAsync();

        return Ok(data);
    }
}