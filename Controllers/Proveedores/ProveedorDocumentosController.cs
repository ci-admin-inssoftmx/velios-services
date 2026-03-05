using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using velios.Api.Models.Common;
using velios.Api.Models.Requests;
using velios.Api.Services.ProveedoresDocs;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador responsable de la gestión de documentos asociados a un proveedor.
///
/// Permite:
/// - Subir documentos (multipart/form-data)
/// - Listar documentos activos
/// - Descargar documentos
/// - Eliminar documentos (soft delete)
///
/// Reglas de upload:
/// - Máximo 15 MB
/// - Extensiones permitidas: .pdf, .txt, .jpg, .jpeg
/// - Validación de firma (magic bytes) para PDF/JPEG + heurística básica para TXT
/// </summary>
[ApiController]
[Route("api/Proveedores/{proveedorId:int}/Documentos")]
public class ProveedorDocumentosController : ControllerBase
{
    private readonly IProveedorDocumentService _service;
    private readonly ILogger<ProveedorDocumentosController> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".txt", ".jpg", ".jpeg"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "text/plain",
        "image/jpeg"
    };

    public ProveedorDocumentosController(
        IProveedorDocumentService service,
        ILogger<ProveedorDocumentosController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ============================================================
    // POST: Upload Documento
    // ============================================================

    /// <summary>
    /// Carga un documento para un proveedor.
    ///
    /// POST /api/Proveedores/{proveedorId}/Documentos/Upload?tipoDocumentoId=1
    ///
    /// multipart/form-data:
    /// - FechaFinVigencia (opcional)
    /// - File (obligatorio)
    /// </summary>
    [HttpPost("Upload")]
    [AllowAnonymous]
    [RequestSizeLimit(15 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    public async Task<ActionResult<ApiResponse<object>>> Upload(
        [FromRoute] int proveedorId,
        [FromQuery] int tipoDocumentoId,
        [FromForm] UploadProveedorDocumentoRequest request)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation(
                "UPLOAD HIT proveedorId={ProveedorId} tipoDocumentoId={TipoDocumentoId} fileNull={FileNull} len={Len} fecha={Fecha}",
                proveedorId,
                tipoDocumentoId,
                request?.File == null,
                request?.File?.Length ?? 0,
                request?.FechaFinVigencia);

            if (proveedorId <= 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "proveedorId es obligatorio.",
                    statusCode = 400
                });
            }

            if (tipoDocumentoId <= 0 || request?.File == null || request.File.Length == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "tipoDocumentoId y archivo son obligatorios.",
                    statusCode = 400
                });
            }

            // ✅ Validación de extensión + Content-Type
            if (!IsAllowedFile(request.File, out var reason))
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = reason,
                    statusCode = 400
                });
            }

            // ✅ Validación de firma (magic bytes)
            if (!HasValidSignature(request.File, out var sigReason))
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = sigReason,
                    statusCode = 400
                });
            }

            var actor = User?.Identity?.Name ?? "PUBLIC";

            var id = await _service.UploadAsync(
                proveedorId,
                tipoDocumentoId,
                request.File,
                request.FechaFinVigencia,
                actor);

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Documento cargado.",
                statusCode = 200,
                data = new { ProveedorDocumentoId = id }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar documento (requestId={RequestId})", requestId);

            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al cargar documento.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    // ============================================================
    // GET: Listar Documentos
    // ============================================================

    /// <summary>
    /// Devuelve la lista de documentos activos de un proveedor.
    /// No incluye eliminados (soft delete).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [Produces("application/json")]
    public async Task<ActionResult<ApiResponse<object>>> List([FromRoute] int proveedorId)
    {
        var requestId = Guid.NewGuid().ToString();
        var items = await _service.ListAsync(proveedorId);

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "OK",
            statusCode = 200,
            data = new { total = items.Count, items }
        });
    }

    // ============================================================
    // GET: Descargar Documento
    // ============================================================

    /// <summary>
    /// Descarga el archivo físico asociado a un documento.
    /// </summary>
    [HttpGet("{docId:long}/Download")]
    [AllowAnonymous]
    public async Task<IActionResult> Download([FromRoute] int proveedorId, [FromRoute] long docId)
    {
        var result = await _service.DownloadAsync(docId);
        if (result == null) return NotFound();
        return File(result.Value.bytes, result.Value.contentType, result.Value.fileName);
    }

    // ============================================================
    // DELETE: Eliminar Documento (Soft Delete)
    // ============================================================

    /// <summary>
    /// Elimina un documento mediante soft delete.
    /// </summary>
    [HttpDelete("{docId:long}")]
    [AllowAnonymous]
    [Produces("application/json")]
    public async Task<ActionResult<ApiResponse<object>>> Delete([FromRoute] int proveedorId, [FromRoute] long docId)
    {
        var requestId = Guid.NewGuid().ToString();
        var ok = await _service.DeleteAsync(docId, User?.Identity?.Name ?? "API");

        if (!ok)
        {
            return NotFound(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Documento no encontrado.",
                statusCode = 404
            });
        }

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Documento eliminado.",
            statusCode = 200
        });
    }

    // ============================================================
    // Helpers: Validaciones
    // ============================================================

    private static bool IsAllowedFile(IFormFile file, out string reason)
    {
        reason = string.Empty;

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            reason = $"Extensión no permitida: {ext}. Permitidas: pdf, txt, jpg, jpeg.";
            return false;
        }

        // Soft-check: si viene informado y no coincide, rechazamos.
        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
        {
            reason = $"Content-Type no permitido: {file.ContentType}.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Valida firma (magic bytes) para PDF/JPEG y heurística básica para TXT.
    /// </summary>
    private static bool HasValidSignature(IFormFile file, out string reason)
    {
        reason = string.Empty;

        Span<byte> header = stackalloc byte[8];
        using var stream = file.OpenReadStream();
        var read = stream.Read(header);

        if (read < 4)
        {
            reason = "Archivo inválido o corrupto.";
            return false;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        // PDF: %PDF
        if (ext == ".pdf")
        {
            if (!(header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F'))
            {
                reason = "El archivo no parece ser un PDF válido.";
                return false;
            }
        }

        // JPG/JPEG: FF D8 FF
        if (ext is ".jpg" or ".jpeg")
        {
            if (!(header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF))
            {
                reason = "El archivo no parece ser un JPEG válido.";
                return false;
            }
        }

        // TXT: no tiene firma oficial; rechazamos si parece binario (byte 0x00)
        if (ext == ".txt")
        {
            if (header.Slice(0, read).Contains((byte)0x00))
            {
                reason = "El .txt parece binario (no permitido).";
                return false;
            }
        }

        return true;
    }
}