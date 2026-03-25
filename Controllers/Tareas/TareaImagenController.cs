using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;

namespace velios.Api.Controllers.Tareas
{
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

        private string GetRootPath() => _config["Storage:ProveedorDocsRoot"] ?? throw new Exception("Configuración faltante.");
        private string GetBaseUrl() => _config["AppSettings:BaseUrl"] ?? throw new Exception("Configuración faltante.");

        // =========================================================
        // POST: Sube una imagen física y retorna la URL generada
        // =========================================================
        [HttpPost("{taskCode}/upload")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<object>>> UploadImagen(string taskCode, IFormFile file)
        {
            var requestId = Guid.NewGuid().ToString();
            try
            {
                if (file == null || file.Length == 0) return BadRequest(new ApiResponse<object> { request_id = requestId, message = "Archivo inválido", statusCode = 400 });

                var tarea = await _db.Tareas.FirstOrDefaultAsync(x => x.TaskCode == taskCode && !x.IsDeleted);
                if (tarea == null) return NotFound(new ApiResponse<object> { request_id = requestId, message = "Tarea no encontrada", statusCode = 404 });

                var root = GetRootPath();
                var baseUrl = GetBaseUrl();
                var folder = Path.Combine(root, taskCode);

                if (!Directory.Exists(folder)) Directory.Exists(Directory.CreateDirectory(folder).FullName);

                var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
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
                    message = "Imagen subida. Copie la URL para el PUT final.",
                    statusCode = 200,
                    data = new { tarea.TareaId, tarea.TaskCode, imagenUrl = finalUrl }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { request_id = requestId, message = "Error", errors = new List<string> { ex.Message } });
            }
        }

        // =========================================================
        // GET: Obtiene la lista de imágenes de la tarea
        // =========================================================
        [HttpGet("{taskCode}")]
        public async Task<ActionResult<ApiResponse<object>>> GetImagenes(string taskCode)
        {
            var requestId = Guid.NewGuid().ToString();
            var tarea = await _db.Tareas.AsNoTracking().FirstOrDefaultAsync(x => x.TaskCode == taskCode && !x.IsDeleted);

            if (tarea == null) return NotFound(new ApiResponse<object> { request_id = requestId, message = "Tarea no encontrada" });

            // Si hay URLs guardadas (separadas por coma), las convertimos en una lista real
            var listaUrls = string.IsNullOrEmpty(tarea.ImagenUrl)
                            ? new List<string>()
                            : tarea.ImagenUrl.Split(',').ToList();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Consulta exitosa.",
                data = new
                {
                    tarea.TareaId,
                    tarea.TaskCode,
                    imagenes = listaUrls,
                    total = listaUrls.Count
                }
            });
        }

        // =========================================================
        // PUT: Recibe todas las URLs y las guarda de forma definitiva
        // =========================================================
        [HttpPut("{taskCode}/finalizar")]
        public async Task<ActionResult<ApiResponse<object>>> FinalizarSubida(string taskCode, [FromBody] TaskImagesUpdateDto model)
        {
            var requestId = Guid.NewGuid().ToString();
            var tarea = await _db.Tareas.FirstOrDefaultAsync(x => x.TaskCode == taskCode && !x.IsDeleted);

            if (tarea == null) return NotFound(new ApiResponse<object> { request_id = requestId, message = "Tarea no encontrada" });

            // Guardamos todas las URLs enviadas por el jefe separadas por coma
            tarea.ImagenUrl = string.Join(",", model.ImagenesUrls);
            tarea.DateModified = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Tarea actualizada correctamente.",
                data = new { tarea.TaskCode, imagenesGuardadas = model.ImagenesUrls }
            });
        }
    }

    public class TaskImagesUpdateDto
    {
        public List<string> ImagenesUrls { get; set; } = new List<string>();
    }
}