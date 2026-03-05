using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using velios.Api.Models.Common;
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
/// La información binaria se almacena en FileSystem y la metadata en base de datos.
/// </summary>
[ApiController]
[Route("api/Proveedores/{proveedorId:int}/Documentos")]
public class ProveedorDocumentosController : ControllerBase
{
    private readonly IProveedorDocumentService _service;

    /// <summary>
    /// Constructor del controlador.
    /// </summary>
    /// <param name="service">
    /// Servicio de negocio encargado del almacenamiento físico y registro
    /// de metadata de documentos de proveedores.
    /// </param>
    public ProveedorDocumentosController(IProveedorDocumentService service)
    {
        _service = service;
    }

    // ============================================================
    // POST: Upload Documento
    // ============================================================

    /// <summary>
    /// Carga un documento para un proveedor.
    /// 
    /// El archivo debe enviarse como multipart/form-data.
    /// Se valida:
    /// - Proveedor existente
    /// - Tamaño máximo permitido (15MB)
    /// - TipoDocumentoId válido
    /// 
    /// Si ya existe un documento activo del mismo tipo,
    /// el anterior se marca como eliminado (soft delete).
    /// </summary>
    /// <param name="proveedorId">Identificador del proveedor.</param>
    /// <param name="tipoDocumentoId">Tipo de documento (INE, RFC, etc.).</param>
    /// <param name="file">Archivo a cargar.</param>
    /// <returns>ApiResponse con el ID del documento creado.</returns>
    /// <response code="200">Documento cargado correctamente.</response>
    /// <response code="400">Datos inválidos o error en proceso.</response>
    [HttpPost("Upload")]
    [AllowAnonymous]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<object>>> Upload(
           [FromRoute] int proveedorId,
           [FromQuery] int tipoDocumentoId,
           [FromForm] DateTime? fechaFinVigencia,
           [FromForm] IFormFile file)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            Console.WriteLine($"UPLOAD HIT proveedorId={proveedorId} tipoDocumentoId={tipoDocumentoId} fileNull={(file == null)} len={(file?.Length ?? 0)} fecha={fechaFinVigencia}");
            if (tipoDocumentoId <= 0 || file == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "tipoDocumentoId y archivo son obligatorios.",
                    statusCode = 400
                });
            }

            var actor = User?.Identity?.Name ?? "PUBLIC";

            // Firma nueva: incluye fechaFinVigencia
            var id = await _service.UploadAsync(proveedorId, tipoDocumentoId, file, fechaFinVigencia, actor);

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
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = $"INVALID: proveedorId={proveedorId}, tipoDocumentoId={tipoDocumentoId}, fileNull={(file == null)}, len={(file?.Length ?? 0)}, fecha={fechaFinVigencia}",
                statusCode = 400
            });
        }
    }
    // ============================================================
    // GET: Listar Documentos
    // ============================================================

    /// <summary>
    /// Devuelve la lista de documentos activos de un proveedor.
    /// No incluye archivos eliminados (IsDeleted = true).
    /// </summary>
    /// <param name="proveedorId">Identificador del proveedor.</param>
    /// <returns>Listado de documentos con metadata.</returns>
    /// <response code="200">Lista obtenida correctamente.</response>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> List(int proveedorId)
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
    /// <param name="proveedorId">Identificador del proveedor (ruta).</param>
    /// <param name="docId">Identificador del documento.</param>
    /// <returns>Archivo binario.</returns>
    /// <response code="200">Archivo descargado.</response>
    /// <response code="404">Documento no encontrado.</response>
    [HttpGet("{docId:long}/Download")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(int proveedorId, long docId)
    {
        var result = await _service.DownloadAsync(docId);
        if (result == null) return NotFound();

        return File(result.Value.bytes, result.Value.contentType, result.Value.fileName);
    }

    // ============================================================
    // DELETE: Eliminar Documento (Soft Delete)
    // ============================================================

    /// <summary>
    /// Elimina un documento de un proveedor mediante soft delete.
    /// No elimina el archivo físico, solo marca IsDeleted = true.
    /// </summary>
    /// <param name="proveedorId">Identificador del proveedor.</param>
    /// <param name="docId">Identificador del documento.</param>
    /// <returns>ApiResponse indicando resultado.</returns>
    /// <response code="200">Documento eliminado correctamente.</response>
    /// <response code="404">Documento no encontrado.</response>
    [HttpDelete("{docId:long}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int proveedorId, long docId)
    {
        var requestId = Guid.NewGuid().ToString();
        var ok = await _service.DeleteAsync(docId, User?.Identity?.Name ?? "API");

        if (!ok)
            return NotFound(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Documento no encontrado.",
                statusCode = 404
            });

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Documento eliminado.",
            statusCode = 200
        });
    }
}