using Microsoft.AspNetCore.Http;

namespace velios.Api.Models.Requests;

/// <summary>
/// Modelo para recibir multipart/form-data del upload.
/// Swagger lo interpreta correctamente.
/// </summary>
public class UploadProveedorDocumentoRequest
{
    /// <summary>
    /// Fecha de fin de vigencia del documento (opcional).
    /// </summary>
    public DateTime? FechaFinVigencia { get; set; }

    /// <summary>
    /// Archivo a subir (obligatorio).
    /// </summary>
    public IFormFile File { get; set; } = default!;
}