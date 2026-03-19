using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;

namespace velios.Api.Controllers.Tareas
{
    /// <summary>
    /// Controlador encargado de la gestión de la IMAGEN de tareas.
    ///
    /// Permite:
    /// - Subir imagen
    /// - Consultar imagen
    /// - Modificar imagen
    /// - Eliminar imagen
    ///
    /// Los archivos se almacenan en disco en la ruta configurada en:
    /// appsettings.json -> Storage:TareaImagenesRoot
    ///
    /// Ejemplo:
    /// D:\archivosVelios\archivos\ImagenesTareas\dev\42\imagen.png
    ///
    /// En base de datos se guarda la URL completa:
    /// {BaseUrl}/ImagenesTareas/{tareaId}/imagen.png
    /// </summary>
    [ApiController]
    [Route("api/tarea-imagen")]
    public class TareaImagenController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public TareaImagenController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        /// <summary>
        /// Obtiene la ruta raíz de imágenes de tareas definida en appsettings.json
        /// </summary>
        private string GetRootPath()
        {
            var root = _config["Storage:TareaImagenesRoot"];

            if (string.IsNullOrWhiteSpace(root))
                throw new Exception("TareaImagenesRoot no está configurado en appsettings.json");

            return root;
        }

        /// <summary>
        /// Obtiene la URL base definida en appsettings.json
        /// </summary>
        private string GetRootArchivoPath()
        {
            var root = _config["AppSettings:BaseUrl"];

            if (string.IsNullOrWhiteSpace(root))
                throw new Exception("BaseUrl no está configurado en appsettings.json");

            return root;
        }

        // =========================================================
        // POST /api/tarea-imagen/{tareaId}/upload
        // =========================================================

        /// <summary>
        /// Permite subir la imagen de una tarea.
        /// </summary>
        [HttpPost("{tareaId}/upload")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<object>>> UploadImagen(long tareaId, IFormFile file)
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

                var tarea = await _db.Tareas
                    .FirstOrDefaultAsync(x => x.TareaId == tareaId && !x.IsDeleted);

                if (tarea == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        request_id = requestId,
                        success = false,
                        message = "Tarea no encontrada.",
                        statusCode = 400
                    });
                }

                var root = GetRootPath();
                var rootArchivos = GetRootArchivoPath();

                var folder = Path.Combine(root, tareaId.ToString());

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var extension = Path.GetExtension(file.FileName);
                var fileName = $"imagen{extension}";
                var filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                tarea.ImagenUrl = $"{rootArchivos}/ImagenesTareas/{tareaId}/{fileName}";
                tarea.DateModified = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = true,
                    message = "Imagen subida correctamente.",
                    statusCode = 200,
                    data = new { tarea.ImagenUrl }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Error al subir imagen.",
                    statusCode = 400,
                    errors = new List<string> { ex.Message }
                });
            }
        }

        // =========================================================
        // GET /api/tarea-imagen/{tareaId}
        // =========================================================

        /// <summary>
        /// Obtiene la URL de la imagen de la tarea.
        /// </summary>
        [HttpGet("{tareaId}")]
        public async Task<ActionResult<ApiResponse<object>>> GetImagen(long tareaId)
        {
            var requestId = Guid.NewGuid().ToString();

            var tarea = await _db.Tareas
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TareaId == tareaId && !x.IsDeleted);

            if (tarea == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Tarea no encontrada.",
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
                    tarea.TareaId,
                    tarea.ImagenUrl
                }
            });
        }

        // =========================================================
        // PUT /api/tarea-imagen/{tareaId}
        // =========================================================

        /// <summary>
        /// Permite reemplazar la imagen existente de una tarea.
        /// </summary>
        [HttpPut("{tareaId}")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateImagen(long tareaId, IFormFile file)
        {
            return await UploadImagen(tareaId, file);
        }

        // =========================================================
        // DELETE /api/tarea-imagen/{tareaId}
        // =========================================================

        /// <summary>
        /// Elimina la imagen de la tarea.
        /// </summary>
        [HttpDelete("{tareaId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteImagen(long tareaId)
        {
            var requestId = Guid.NewGuid().ToString();

            try
            {
                var tarea = await _db.Tareas
                    .FirstOrDefaultAsync(x => x.TareaId == tareaId && !x.IsDeleted);

                if (tarea == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        request_id = requestId,
                        success = false,
                        message = "Tarea no encontrada.",
                        statusCode = 400
                    });
                }

                if (string.IsNullOrWhiteSpace(tarea.ImagenUrl))
                {
                    return Ok(new ApiResponse<object>
                    {
                        request_id = requestId,
                        success = true,
                        message = "La tarea no tiene imagen.",
                        statusCode = 200
                    });
                }

                var root = GetRootPath();

                var fullPath = Path.Combine(root, tarea.ImagenUrl.TrimStart('/').Replace("/", "\\"));

                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);

                tarea.ImagenUrl = null;
                tarea.DateModified = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = true,
                    message = "Imagen eliminada.",
                    statusCode = 200
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Error al eliminar imagen.",
                    statusCode = 400,
                    errors = new List<string> { ex.Message }
                });
            }
        }
    }
}