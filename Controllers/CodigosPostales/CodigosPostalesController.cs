using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using velios.Api.Models.Common;
using velios.Api.Services.CodigosPostales;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/Utileria/CodigosPostales")]
public class CodigosPostalesController : ControllerBase
{
    private readonly ICodigoPostalService _service;

    public CodigosPostalesController(ICodigoPostalService service)
    {
        _service = service;
    }

    /// <summary>
    /// Devuelve Estado, Municipio y Colonias por Código Postal.
    /// </summary>
    [HttpGet("{cp}/Info")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetInfo(string cp)
    {
        var requestId = Guid.NewGuid().ToString();

        var result = await _service.GetInfoAsync(cp);

        if (result == null)
        {
            return NotFound(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Código Postal no encontrado.",
                statusCode = 404
            });
        }

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "OK",
            statusCode = 200,
            data = result
        });
    }

    /// <summary>
    /// Autocomplete de colonias dentro de un CP.
    /// </summary>
    [HttpGet("{cp}/Colonias")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Colonias(string cp, [FromQuery] string? q)
    {
        var requestId = Guid.NewGuid().ToString();

        var data = await _service.SearchColoniasAsync(cp, q);

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "OK",
            statusCode = 200,
            data = data
        });
    }
}