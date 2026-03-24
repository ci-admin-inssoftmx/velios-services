using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.CentrosTrabajo.Requests;
using velios.Api.Models.Clientes;
using velios.Api.Models.Common;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CentrosTrabajoController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<CentrosTrabajoController> _logger;

    public CentrosTrabajoController(AppDbContext db, ILogger<CentrosTrabajoController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("Create")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CentroTrabajoCreateRequest model)
    {
        try
        {
            if (model == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "Solicitud inválida.",
                    statusCode = 400
                });
            }

            if (model.ClienteId <= 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "ClienteId es obligatorio.",
                    statusCode = 400
                });
            }

            if (string.IsNullOrWhiteSpace(model.Nombre))
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "Nombre es obligatorio.",
                    statusCode = 400
                });
            }

            var clienteExiste = await _db.Clientes
                .AsNoTracking()
                .AnyAsync(x => x.ClienteId == model.ClienteId && x.IsDeleted == false);

            if (!clienteExiste)
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "El cliente no existe.",
                    statusCode = 400
                });
            }

            var entity = new CentroTrabajo
            {
                ClienteId = model.ClienteId,
                Nombre = model.Nombre.Trim(),
                Estado = string.IsNullOrWhiteSpace(model.Estado) ? "ACTIVO" : model.Estado.Trim(),
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
                TipoGeocerca = model.TipoGeocerca?.Trim(),
                RadioMetros = model.RadioMetros,
                CreatedBy = "SYSTEM",
                DateCreated = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.Set<CentroTrabajo>().Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = "Centro de trabajo creado.",
                statusCode = 200,
                data = MapCentroTrabajo(entity)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear centro de trabajo.");

            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "Error al crear centro de trabajo.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    [HttpGet("List")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> List()
    {
        try
        {
            var data = await _db.Set<CentroTrabajo>()
                .AsNoTracking()
                .Where(x => x.IsDeleted == false)
                .OrderByDescending(x => x.CentroTrabajoId)
                .Select(x => new
                {
                    x.CentroTrabajoId,
                    x.ClienteId,
                    x.Nombre,
                    x.Estado,
                    x.Zona,
                    x.Territorio,
                    x.Region,
                    x.Calle,
                    x.Numero,
                    x.Colonia,
                    x.Municipio,
                    x.CodigoPostal,
                    x.Lat,
                    x.Lng,
                    x.TipoGeocerca,
                    x.RadioMetros,
                    x.CreatedBy,
                    x.ModifiedBy,
                    x.DateCreated,
                    x.DateModified
                })
                .ToListAsync();

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = "Consulta exitosa.",
                statusCode = 200,
                data = data
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar centros de trabajo.");

            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "Error al consultar centros de trabajo.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    [HttpGet("{centroTrabajoId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Get(int centroTrabajoId)
    {
        try
        {
            var entity = await _db.Set<CentroTrabajo>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CentroTrabajoId == centroTrabajoId && x.IsDeleted == false);

            if (entity == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    success = false,
                    message = "Centro de trabajo no encontrado.",
                    statusCode = 404
                });
            }

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = "Consulta exitosa.",
                statusCode = 200,
                data = MapCentroTrabajo(entity)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar centro de trabajo {CentroTrabajoId}.", centroTrabajoId);

            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "Error al consultar centro de trabajo.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    [HttpPut("{centroTrabajoId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        int centroTrabajoId,
        [FromBody] CentroTrabajoUpdateRequest model)
    {
        try
        {
            if (model == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "Solicitud inválida.",
                    statusCode = 400
                });
            }

            var entity = await _db.Set<CentroTrabajo>()
                .FirstOrDefaultAsync(x => x.CentroTrabajoId == centroTrabajoId && x.IsDeleted == false);

            if (entity == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    success = false,
                    message = "Centro de trabajo no encontrado.",
                    statusCode = 404
                });
            }

            var clienteExiste = await _db.Clientes
                .AsNoTracking()
                .AnyAsync(x => x.ClienteId == model.ClienteId && x.IsDeleted == false);

            if (!clienteExiste)
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "El cliente no existe.",
                    statusCode = 400
                });
            }

            if (string.IsNullOrWhiteSpace(model.Nombre))
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "Nombre es obligatorio.",
                    statusCode = 400
                });
            }

            entity.ClienteId = model.ClienteId;
            entity.Nombre = model.Nombre.Trim();
            entity.Estado = model.Estado?.Trim() ?? entity.Estado;
            entity.Zona = model.Zona?.Trim();
            entity.Territorio = model.Territorio?.Trim();
            entity.Region = model.Region?.Trim();
            entity.Calle = model.Calle?.Trim();
            entity.Numero = model.Numero?.Trim();
            entity.Colonia = model.Colonia?.Trim();
            entity.Municipio = model.Municipio?.Trim();
            entity.CodigoPostal = model.CodigoPostal?.Trim();
            entity.Lat = model.Lat;
            entity.Lng = model.Lng;
            entity.TipoGeocerca = model.TipoGeocerca?.Trim();
            entity.RadioMetros = model.RadioMetros;
            entity.ModifiedBy = "SYSTEM";
            entity.DateModified = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = "Centro de trabajo actualizado.",
                statusCode = 200,
                data = MapCentroTrabajo(entity)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar centro de trabajo {CentroTrabajoId}.", centroTrabajoId);

            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "Error al actualizar centro de trabajo.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    [HttpDelete("{centroTrabajoId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int centroTrabajoId)
    {
        try
        {
            var entity = await _db.Set<CentroTrabajo>()
                .FirstOrDefaultAsync(x => x.CentroTrabajoId == centroTrabajoId && x.IsDeleted == false);

            if (entity == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    success = false,
                    message = "Centro de trabajo no encontrado.",
                    statusCode = 404
                });
            }

            entity.IsDeleted = true;
            entity.ModifiedBy = "SYSTEM";
            entity.DateModified = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = "Centro de trabajo eliminado correctamente.",
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar centro de trabajo {CentroTrabajoId}.", centroTrabajoId);

            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "Error al eliminar centro de trabajo.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    [HttpPost("{centroTrabajoId:int}/Activar")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Activar(int centroTrabajoId)
        => await SetEstado(centroTrabajoId, "ACTIVO", "Centro de trabajo activado.");

    [HttpPost("{centroTrabajoId:int}/Suspender")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Suspender(int centroTrabajoId)
        => await SetEstado(centroTrabajoId, "SUSPENDIDO", "Centro de trabajo suspendido.");

    [HttpPost("{centroTrabajoId:int}/Baja")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Baja(int centroTrabajoId)
        => await SetEstado(centroTrabajoId, "BAJA", "Centro de trabajo dado de baja.");

    private async Task<ActionResult<ApiResponse<object>>> SetEstado(
        int centroTrabajoId,
        string estado,
        string msg)
    {
        try
        {
            var entity = await _db.Set<CentroTrabajo>()
                .FirstOrDefaultAsync(x => x.CentroTrabajoId == centroTrabajoId && x.IsDeleted == false);

            if (entity == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    success = false,
                    message = "Centro de trabajo no encontrado.",
                    statusCode = 404
                });
            }

            entity.Estado = estado;
            entity.ModifiedBy = "SYSTEM";
            entity.DateModified = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = msg,
                statusCode = 200,
                data = new
                {
                    entity.CentroTrabajoId,
                    entity.Estado
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estado del centro de trabajo {CentroTrabajoId}.", centroTrabajoId);

            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "Error al cambiar estado del centro de trabajo.",
                statusCode = 400,
                errors = GetErrorMessages(ex)
            });
        }
    }

    private static object MapCentroTrabajo(CentroTrabajo entity) => new
    {
        entity.CentroTrabajoId,
        entity.ClienteId,
        entity.Nombre,
        entity.Estado,
        entity.Zona,
        entity.Territorio,
        entity.Region,
        entity.Calle,
        entity.Numero,
        entity.Colonia,
        entity.Municipio,
        entity.CodigoPostal,
        entity.Lat,
        entity.Lng,
        entity.TipoGeocerca,
        entity.RadioMetros,
        entity.CreatedBy,
        entity.ModifiedBy,
        entity.DateCreated,
        entity.DateModified
    };

    private static List<string> GetErrorMessages(Exception ex)
    {
        var errors = new List<string>();
        var current = ex;

        while (current != null)
        {
            errors.Add(current.Message);
            current = current.InnerException;
        }

        return errors;
    }
}