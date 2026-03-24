using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Clientes;
using velios.Api.Models.Clientes.Requests;
using velios.Api.Models.Common;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CentrosTrabajoController : ControllerBase
{
    private readonly AppDbContext _db;
    public CentrosTrabajoController(AppDbContext db) => _db = db;

    [HttpPost("Create")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CentroTrabajoCreateRequest model)
    {
        

        var clienteOk = await _db.Clientes.AsNoTracking().AnyAsync(c => c.ClienteId == model.ClienteId && c.EstatusClienteId == 1 && c.IsDeleted == false);
        if (!clienteOk)
            return BadRequest(new ApiResponse<object> {  success = false, message = "Cliente inválido o no activo.", statusCode = 400 });

        var nombre = (model.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(new ApiResponse<object> {  success = false, message = "Nombre es obligatorio.", statusCode = 400 });

        var entity = new CentroTrabajo
        {
            ClienteId = model.ClienteId,
            Nombre = nombre,
            Estado = model.Estado?.Trim(),
            Zona = model.Zona?.Trim(),
            Territorio = model.Territorio?.Trim(),
            Region = model.Region?.Trim(),
            Calle = model.Calle?.Trim(),
            Numero = model.Numero?.Trim(),
            Colonia = model.Colonia?.Trim(),
            Municipio = model.Municipio?.Trim(),
            CodigoPostal = model.CodigoPostal?.Trim(),
            Lat = model.Lat,
            Lng = model.Lng,
            CreatedBy = User?.Identity?.Name ?? "SYSTEM",
            DateCreated = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.CentrosTrabajo.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object> {  success = true, message = "Centro de trabajo creado.", statusCode = 200, data = new { entity.CentroTrabajoId } });
    }

    [HttpGet("ByCliente/{clienteId:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ListByCliente(int clienteId)
    {
        

        var data = await _db.CentrosTrabajo.AsNoTracking()
            .Where(x => x.ClienteId == clienteId && x.IsDeleted == false)
            .OrderByDescending(x => x.CentroTrabajoId)
            .Select(x => new { x.CentroTrabajoId, x.Nombre, x.Estado, x.Zona, x.Territorio, x.Region, x.Lat, x.Lng, x.TipoGeocerca, x.RadioMetros })
            .ToListAsync();

        return Ok(new ApiResponse<object>
        {
            
            success = true,
            message = "OK",
            statusCode = 200,
            data = data
        });
    }

    [HttpPost("{centroId:int}/Geocerca/Radio")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> SetRadio(int centroId, [FromBody] GeocercaRadioRequest model)
    {
        

        if (model.RadioMetros <= 0)
            return BadRequest(new ApiResponse<object> {  success = false, message = "RadioMetros debe ser > 0.", statusCode = 400 });

        var centro = await _db.CentrosTrabajo.FirstOrDefaultAsync(x => x.CentroTrabajoId == centroId && x.IsDeleted == false);
        if (centro == null)
            return NotFound(new ApiResponse<object> {  success = false, message = "Centro no encontrado.", statusCode = 404 });

        // Limpia polígono anterior (soft delete)
        var puntos = await _db.CentroTrabajoPoligonos.Where(p => p.CentroTrabajoId == centroId && p.IsDeleted == false).ToListAsync();
        foreach (var p in puntos) p.IsDeleted = true;

        centro.TipoGeocerca = "RADIO";
        centro.Lat = model.Lat;
        centro.Lng = model.Lng;
        centro.RadioMetros = model.RadioMetros;
        centro.DateModified = DateTime.UtcNow;
        centro.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object> {  success = true, message = "Geocerca por radio configurada.", statusCode = 200 });
    }

    [HttpPost("{centroId:int}/Geocerca/Poligono")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> SetPoligono(int centroId, [FromBody] GeocercaPoligonoRequest model)
    {
        

        if (model.Puntos == null || model.Puntos.Count < 3)
            return BadRequest(new ApiResponse<object> {  success = false, message = "El polígono requiere al menos 3 puntos.", statusCode = 400 });

        var centro = await _db.CentrosTrabajo.FirstOrDefaultAsync(x => x.CentroTrabajoId == centroId && x.IsDeleted == false);
        if (centro == null)
            return NotFound(new ApiResponse<object> {  success = false, message = "Centro no encontrado.", statusCode = 404 });

        // Limpia radio
        centro.RadioMetros = null;

        // Borra (soft delete) puntos anteriores
        var prev = await _db.CentroTrabajoPoligonos.Where(p => p.CentroTrabajoId == centroId && p.IsDeleted == false).ToListAsync();
        foreach (var p in prev) p.IsDeleted = true;

        // Inserta nuevos puntos
        foreach (var pt in model.Puntos.OrderBy(x => x.Orden))
        {
            _db.CentroTrabajoPoligonos.Add(new CentroTrabajoPoligono
            {
                CentroTrabajoId = centroId,
                Orden = pt.Orden,
                Lat = pt.Lat,
                Lng = pt.Lng,
                CreatedBy = User?.Identity?.Name ?? "SYSTEM",
                DateCreated = DateTime.UtcNow,
                IsDeleted = false
            });
        }

        centro.TipoGeocerca = "POLIGONO";
        centro.DateModified = DateTime.UtcNow;
        centro.ModifiedBy = User?.Identity?.Name ?? "SYSTEM";

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object> {  success = true, message = "Geocerca por polígono configurada.", statusCode = 200 });
    }
}