using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Proveedores;

namespace velios.Api.Services.ProveedoresDocs;

/// <summary>
/// Servicio encargado de gestionar el ciclo de vida de documentos de proveedores:
/// - Validación de reglas por tipo de documento (catálogo)
/// - Almacenamiento físico en FileSystem
/// - Persistencia de metadata en base de datos
/// - Descarga y eliminación lógica (soft delete)
///
/// Diseño:
/// - El archivo se guarda en disco (Storage:ProveedorDocsRoot)
/// - La metadata se guarda en dbo.tb_ProveedorDocumentos
/// - El tipo de documento y reglas se obtienen de dbo.tb_CatTipoDocumentoProveedor
/// </summary>
public class ProveedorDocumentService : IProveedorDocumentService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    /// <summary>
    /// Constructor del servicio.
    /// </summary>
    /// <param name="db">DbContext de la aplicación.</param>
    /// <param name="config">Configuración (appsettings) para obtener el path de almacenamiento.</param>
    public ProveedorDocumentService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// Sube (guarda) un documento de proveedor.
    ///
    /// Flujo:
    /// 1) Valida proveedor existe (y no está eliminado)
    /// 2) Valida tipo de documento existe/activo (catálogo) y aplica reglas:
    ///    - Tamaño máximo
    ///    - Formatos permitidos (PDF/Imagen)
    ///    - Requiere vigencia => FechaFinVigencia obligatoria y futura
    /// 3) Guarda archivo en disco (ruta: proveedores/{proveedorId}/{yyyy}/{MM}/GUID.ext)
    /// 4) Calcula SHA256 del archivo durante la escritura
    /// 5) Soft delete del documento anterior del mismo tipo (si existía)
    /// 6) Inserta registro de metadata en tb_ProveedorDocumentos
    /// </summary>
    /// <param name="proveedorId">Proveedor dueño del documento.</param>
    /// <param name="tipoDocumentoId">Tipo de documento (catálogo).</param>
    /// <param name="file">Archivo subido (multipart/form-data).</param>
    /// <param name="fechaFinVigencia">Fecha de fin de vigencia (obligatoria si el tipo la requiere).</param>
    /// <param name="actor">Usuario/actor que ejecuta la acción (auditoría).</param>
    /// <returns>ID del documento creado (ProveedorDocumentoId).</returns>
    public async Task<long> UploadAsync(
        int proveedorId,
        int tipoDocumentoId,
        IFormFile file,
        DateTime? fechaFinVigencia,
        string? actor)
    {
        // --------------- Validaciones básicas ---------------
        if (proveedorId <= 0) throw new InvalidOperationException("ProveedorId inválido.");
        if (tipoDocumentoId <= 0) throw new InvalidOperationException("TipoDocumentoId inválido.");
        if (file == null || file.Length <= 0) throw new InvalidOperationException("Archivo inválido.");

        var now = DateTime.UtcNow;
        actor ??= "API";

        // --------------- Validar proveedor ---------------
        var proveedorOk = await _db.Proveedores.AsNoTracking()
            .AnyAsync(p => p.ProveedorId == proveedorId && p.IsDeleted != true);

        if (!proveedorOk)
            throw new InvalidOperationException("Proveedor inválido o eliminado.");

        // --------------- Validar tipo documento + reglas ---------------
        var tipo = await _db.CatTipoDocumentoProveedor.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TipoDocumentoId == tipoDocumentoId && t.Activo && !t.IsDeleted);

        if (tipo == null)
            throw new InvalidOperationException("TipoDocumentoId inválido o inactivo.");

        // Vigencia obligatoria por tipo
        if (tipo.RequiereVigencia)
        {
            if (fechaFinVigencia == null)
                throw new InvalidOperationException("Este tipo de documento requiere FechaFinVigencia.");

            if (fechaFinVigencia.Value.Date <= now.Date)
                throw new InvalidOperationException("La FechaFinVigencia debe ser futura.");
        }

        // Tamaño máximo por tipo (si viene en catálogo)
        if (tipo.MaxBytes > 0 && file.Length > tipo.MaxBytes)
            throw new InvalidOperationException($"El archivo excede el tamaño máximo permitido ({tipo.MaxBytes} bytes).");

        // Formatos permitidos por tipo
        var originalName = Path.GetFileName(file.FileName);
        var ext = (Path.GetExtension(originalName) ?? "").ToLowerInvariant();
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType.Trim();

        var isPdf = ext == ".pdf" || contentType.ToLowerInvariant().Contains("pdf");
        var isImg = ext == ".jpg" || ext == ".jpeg" || ext == ".png";

        if (isPdf && !tipo.PermitePdf)
            throw new InvalidOperationException("Este tipo de documento no permite PDF.");

        if (isImg && !tipo.PermiteImagen)
            throw new InvalidOperationException("Este tipo de documento no permite imágenes (JPG/PNG).");

        if (!isPdf && !isImg)
            throw new InvalidOperationException("Formato no permitido. Solo PDF/JPG/PNG.");

        // --------------- Preparar ruta destino ---------------
        var root = _config["Storage:ProveedorDocsRoot"];
        var rootArchivo = _config["AppSettings:BaseUrl"];

        if (string.IsNullOrWhiteSpace(root))
            throw new Exception("Falta configuración: Storage:ProveedorDocsRoot");

        var storedName = $"{Guid.NewGuid():N}{ext}";
        var relDir = Path.Combine("proveedores", proveedorId.ToString(), now.Year.ToString(), now.Month.ToString("00"));
        var fullDir = Path.Combine(root, relDir);
        Directory.CreateDirectory(fullDir);

        var fullPath = Path.Combine(fullDir, storedName);

        // --------------- Guardar archivo + calcular SHA256 ---------------
        string sha256Hex;
        using (var sha = SHA256.Create())
        {
            await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var input = file.OpenReadStream();

            // Copia en chunks, hasheando sin cargar todo en memoria
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                sha.TransformBlock(buffer, 0, read, null, 0);
                await output.WriteAsync(buffer, 0, read);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha256Hex = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }

        // --------------- Soft delete del documento anterior (mismo tipo) ---------------
        // Si tienes índice único por ProveedorId+TipoDocumentoId (activos), esto evita conflicto
        var prev = await _db.ProveedorDocumentos
            .FirstOrDefaultAsync(d => d.ProveedorId == proveedorId
                                   && d.TipoDocumentoId == tipoDocumentoId
                                   && !d.IsDeleted);

        if (prev != null)
        {
            prev.IsDeleted = true;
            prev.DateModified = now;
            prev.ModifiedBy = actor;
        }

        // --------------- Registrar metadata ---------------
        var entity = new ProveedorDocumento
        {
            ProveedorId = proveedorId,
            TipoDocumentoId = tipoDocumentoId,

            NombreOriginal = originalName,
            NombreAlmacenado = storedName,
            Extension = ext,
            ContentType = contentType,
            TamanoBytes = file.Length,
            Sha256 = sha256Hex,

            RutaRelativa = rootArchivo +"/"+ Path.Combine(relDir, storedName).Replace("\\", "/"),
            EstatusDocumentoId = 1, // 1=SUBIDO (ajusta según tu catálogo)
            Observaciones = null,

            FechaFinVigencia = fechaFinVigencia?.Date,

            CreatedBy = actor,
            DateCreated = now,
            IsDeleted = false
        };

        _db.ProveedorDocumentos.Add(entity);
        await _db.SaveChangesAsync();

        return entity.ProveedorDocumentoId;
    }

    /// <summary>
    /// Descarga un documento por su ID (ProveedorDocumentoId).
    /// </summary>
    /// <param name="proveedorDocumentoId">ID del documento.</param>
    /// <returns>
    /// Tuple con:
    /// - bytes del archivo
    /// - contentType
    /// - fileName (nombre original)
    /// o null si no existe o está eliminado.
    /// </returns>
    public async Task<(byte[] bytes, string contentType, string fileName)?> DownloadAsync(long proveedorDocumentoId)
    {
        var doc = await _db.ProveedorDocumentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ProveedorDocumentoId == proveedorDocumentoId && !d.IsDeleted);

        if (doc == null) return null;

        var root = _config["Storage:ProveedorDocsRoot"];
        if (string.IsNullOrWhiteSpace(root))
            throw new Exception("Falta configuración: Storage:ProveedorDocsRoot");

        var fullPath = Path.Combine(root, doc.RutaRelativa.Replace("/", "\\"));

        if (!File.Exists(fullPath)) return null;

        var bytes = await File.ReadAllBytesAsync(fullPath);
        return (bytes, doc.ContentType, doc.NombreOriginal);
    }

    /// <summary>
    /// Lista documentos activos (no eliminados) de un proveedor.
    /// </summary>
    /// <param name="proveedorId">Proveedor a consultar.</param>
    /// <returns>Lista con metadata básica.</returns>
    public async Task<List<object>> ListAsync(int proveedorId)
    {
        var items = await _db.ProveedorDocumentos.AsNoTracking()
            .Where(d => d.ProveedorId == proveedorId && !d.IsDeleted)
            .OrderByDescending(d => d.ProveedorDocumentoId)
            .Select(d => new
            {
                d.ProveedorDocumentoId,
                d.TipoDocumentoId,
                d.NombreOriginal,
                d.ContentType,
                d.TamanoBytes,
                d.Sha256,
                d.FechaFinVigencia,
                d.EstatusDocumentoId,
                d.DateCreated
            })
            .ToListAsync();

        return items.Cast<object>().ToList();
    }

    /// <summary>
    /// Elimina un documento mediante soft delete (marca IsDeleted=true).
    /// No borra el archivo físico (decisión intencional por auditoría/recuperación).
    /// </summary>
    /// <param name="proveedorDocumentoId">ID del documento.</param>
    /// <param name="actor">Usuario/actor que ejecuta la acción.</param>
    /// <returns>true si se eliminó; false si no existe.</returns>
    public async Task<bool> DeleteAsync(long proveedorDocumentoId, string? actor)
    {
        actor ??= "API";

        var doc = await _db.ProveedorDocumentos
            .FirstOrDefaultAsync(d => d.ProveedorDocumentoId == proveedorDocumentoId && !d.IsDeleted);

        if (doc == null) return false;

        doc.IsDeleted = true;
        doc.DateModified = DateTime.UtcNow;
        doc.ModifiedBy = actor;

        await _db.SaveChangesAsync();
        return true;
    }
}