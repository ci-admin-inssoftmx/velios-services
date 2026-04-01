using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;

namespace velios.Api.Controllers.Tareas
{
    [ApiController]
    [Route("api/tarea-archivo")]
    public class TareaArchivoController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        // Extensiones permitidas: imágenes + documentos Office + PDF
        private static readonly HashSet<string> _extensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
        {
            // Imágenes
            ".jpg", ".jpeg", ".png",
            // Documentos
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
        };

        // Tamaño máximo por archivo: 20 MB
        private const long MaxFileSizeBytes = 20 * 1024 * 1024;

        public TareaArchivoController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        private string GetRootPath() => _config["Storage:ProveedorDocsRoot"] ?? throw new Exception("Configuración faltante.");
        private string GetBaseUrl() => _config["AppSettings:BaseUrl"] ?? throw new Exception("Configuración faltante.");

        /// <summary>
        /// Devuelve una categoría legible según la extensión del archivo.
        /// </summary>
        private static string ObtenerTipoArchivo(string extension) =>
            extension.ToLower() switch
            {
                ".jpg" or ".jpeg" or ".png" => "imagen",
                ".pdf" => "pdf",
                ".doc" or ".docx" => "word",
                ".xls" or ".xlsx" => "excel",
                ".ppt" or ".pptx" => "powerpoint",
                _ => "desconocido"
            };

        // =========================================================
        // POST: Sube un archivo físico y retorna la URL generada
        //       Admite: jpg, jpeg, png, pdf, doc, docx,
        //               xls, xlsx, ppt, pptx
        // =========================================================
        [HttpPost("{taskCode}/upload")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<object>>> UploadArchivo(string taskCode, IFormFile file)
        {
            var requestId = Guid.NewGuid().ToString();
            try
            {
                // --- Validaciones básicas ---
                if (file == null || file.Length == 0)
                    return BadRequest(new ApiResponse<object>
                    {
                        request_id = requestId,
                        message = "Archivo inválido o vacío.",
                        statusCode = 400
                    });

                if (file.Length > MaxFileSizeBytes)
                    return BadRequest(new ApiResponse<object>
                    {
                        request_id = requestId,
                        message = $"El archivo supera el tamaño máximo permitido de {MaxFileSizeBytes / 1024 / 1024} MB.",
                        statusCode = 400
                    });

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(extension) || !_extensionesPermitidas.Contains(extension))
                    return BadRequest(new ApiResponse<object>
                    {
                        request_id = requestId,
                        message = $"Extensión '{extension}' no permitida. " +
                                  $"Use: {string.Join(", ", _extensionesPermitidas)}.",
                        statusCode = 400
                    });

                // --- Verificar que la tarea existe ---
                var tarea = await _db.Tareas.FirstOrDefaultAsync(x => x.TaskCode == taskCode && !x.IsDeleted);
                if (tarea == null)
                    return NotFound(new ApiResponse<object>
                    {
                        request_id = requestId,
                        message = "Tarea no encontrada.",
                        statusCode = 404
                    });

                // --- Guardar el archivo en disco ---
                var root = GetRootPath();
                var baseUrl = GetBaseUrl();
                var folder = Path.Combine(root, taskCode);

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var uniqueName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(folder, uniqueName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var finalUrl = $"{baseUrl}/{taskCode}/{uniqueName}";

                return Ok(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = true,
                    message = "Archivo subido correctamente. Copie la URL para el PUT final.",
                    statusCode = 200,
                    data = new
                    {
                        tarea.TareaId,
                        tarea.TaskCode,
                        archivoUrl = finalUrl,
                        extension,
                        tipoArchivo = ObtenerTipoArchivo(extension),
                        nombreOriginal = file.FileName
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    message = "Error al subir el archivo.",
                    errors = new List<string> { ex.Message }
                });
            }
        }

        // =========================================================
        // GET: Obtiene la lista de archivos de la tarea
        // =========================================================
        [HttpGet("{taskCode}")]
        public async Task<ActionResult<ApiResponse<object>>> GetArchivos(string taskCode)
        {
            var requestId = Guid.NewGuid().ToString();

            var tarea = await _db.Tareas
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TaskCode == taskCode && !x.IsDeleted);

            if (tarea == null)
                return NotFound(new ApiResponse<object>
                {
                    request_id = requestId,
                    message = "Tarea no encontrada."
                });

            var listaUrls = string.IsNullOrEmpty(tarea.ImagenUrl)
                ? new List<string>()
                : tarea.ImagenUrl.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

            // Enriquecemos cada URL con su extensión y tipo
            var archivosDetalle = listaUrls.Select(url => new
            {
                url,
                extension = Path.GetExtension(url),
                tipoArchivo = ObtenerTipoArchivo(Path.GetExtension(url))
            }).ToList();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Consulta exitosa.",
                data = new
                {
                    tarea.TareaId,
                    tarea.TaskCode,
                    archivos = archivosDetalle,
                    total = archivosDetalle.Count
                }
            });
        }

        // =========================================================
        // PUT: Recibe todas las URLs y las guarda de forma definitiva
        // =========================================================
        [HttpPut("{taskCode}/finalizar")]
        public async Task<ActionResult<ApiResponse<object>>> FinalizarSubida(
            string taskCode,
            [FromBody] TareaArchivosUpdateDto model)
        {
            var requestId = Guid.NewGuid().ToString();

            var tarea = await _db.Tareas
                .FirstOrDefaultAsync(x => x.TaskCode == taskCode && !x.IsDeleted);

            if (tarea == null)
                return NotFound(new ApiResponse<object>
                {
                    request_id = requestId,
                    message = "Tarea no encontrada."
                });

            // Guardar todas las URLs separadas por coma
            tarea.ImagenUrl = string.Join(",", model.ArchivosUrls);
            tarea.DateModified = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Tarea actualizada correctamente.",
                data = new
                {
                    tarea.TaskCode,
                    archivosGuardados = model.ArchivosUrls
                }
            });
        }
    }

    // DTO actualizado
    public class TareaArchivosUpdateDto
    {
        public List<string> ArchivosUrls { get; set; } = new List<string>();
    }
}