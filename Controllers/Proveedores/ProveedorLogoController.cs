using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado de la gestión del LOGO de proveedores.
///
/// Permite:
/// - Subir logo
/// - Consultar logo
/// - Modificar logo
/// - Eliminar logo
///
/// Los archivos se almacenan en disco en la ruta configurada en:
/// appsettings.json -> ProveedorDocsRoot
///
/// Ejemplo:
/// D:\Archivos_Adh_1-0\archivos_cliente\Velios\dev\proveedores\15\logo.png
///
/// En base de datos se guarda la URL relativa:
/// /proveedores/15/logo.png
/// </summary>
[ApiController]
[Route("api/proveedor-logo")]
public class ProveedorLogoController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public ProveedorLogoController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// Obtiene la ruta raíz de almacenamiento definida en appsettings.json
    /// </summary>
    private string GetRootPath()
    {
        var root = _config["Storage:ProveedorDocsRoot"];

        if (string.IsNullOrWhiteSpace(root))
            throw new Exception("ProveedorDocsRoot no está configurado en appsettings.json");

        return root;
    }

    // =========================================================
    // POST /api/proveedor-logo/{proveedorId}/upload
    // =========================================================

    /// <summary>
    /// Permite subir el logo de un proveedor.
    /// </summary>
    [HttpPost("{proveedorId}/upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<object>>> UploadLogo(long proveedorId, IFormFile file)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Archivo inválido.",
                    statusCode = 400
                });
            }

            var proveedor = await _db.Proveedores
                .FirstOrDefaultAsync(x => x.ProveedorId == proveedorId && !x.IsDeleted);

            if (proveedor == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Proveedor no encontrado.",
                    statusCode = 400
                });
            }

            var root = GetRootPath();

            var folder = Path.Combine(root, "proveedores", proveedorId.ToString());

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var extension = Path.GetExtension(file.FileName);

            var fileName = $"logo{extension}";

            var filePath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            proveedor.LogoUrl = $"/proveedores/{proveedorId}/{fileName}";
            proveedor.DateModified = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Logo subido correctamente.",
                statusCode = 200,
                data = new { proveedor.LogoUrl }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al subir logo.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    // =========================================================
    // GET /api/proveedor-logo/{proveedorId}
    // =========================================================

    /// <summary>
    /// Obtiene la URL del logo del proveedor.
    /// </summary>
    [HttpGet("{proveedorId}")]
    public async Task<ActionResult<ApiResponse<object>>> GetLogo(long proveedorId)
    {
        var requestId = Guid.NewGuid().ToString();

        var proveedor = await _db.Proveedores
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProveedorId == proveedorId && !x.IsDeleted);

        if (proveedor == null)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Proveedor no encontrado.",
                statusCode = 400
            });
        }

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Consulta exitosa.",
            statusCode = 200,
            data = new
            {
                proveedor.ProveedorId,
                proveedor.LogoUrl
            }
        });
    }

    // =========================================================
    // PUT /api/proveedor-logo/{proveedorId}
    // =========================================================

    /// <summary>
    /// Permite reemplazar el logo existente.
    /// </summary>
    [HttpPut("{proveedorId}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateLogo(long proveedorId, IFormFile file)
    {
        return await UploadLogo(proveedorId, file);
    }

    // =========================================================
    // DELETE /api/proveedor-logo/{proveedorId}
    // =========================================================

    /// <summary>
    /// Elimina el logo del proveedor.
    /// </summary>
    [HttpDelete("{proveedorId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteLogo(long proveedorId)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            var proveedor = await _db.Proveedores
                .FirstOrDefaultAsync(x => x.ProveedorId == proveedorId && !x.IsDeleted);

            if (proveedor == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Proveedor no encontrado.",
                    statusCode = 400
                });
            }

            if (string.IsNullOrWhiteSpace(proveedor.LogoUrl))
            {
                return Ok(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = true,
                    message = "El proveedor no tiene logo.",
                    statusCode = 200
                });
            }

            var root = GetRootPath();

            var fullPath = Path.Combine(root, proveedor.LogoUrl.TrimStart('/').Replace("/", "\\"));

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);

            proveedor.LogoUrl = null;
            proveedor.DateModified = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Logo eliminado.",
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al eliminar logo.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }
}