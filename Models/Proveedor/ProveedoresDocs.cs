using Microsoft.AspNetCore.Http;

namespace velios.Api.Models.ProveedoresDocs
{
    /// <summary>
    /// Modelo para recibir multipart/form-data en Swagger y en el binder.
    /// Nota: usamos string para fecha para evitar problemas de Swagger con DateTime? en form-data.
    /// </summary>
    public class UploadDocumentoRequest
    {
        /// <summary>
        /// Fecha fin de vigencia en formato ISO o yyyy-MM-dd (opcional).
        /// Ej: 2026-03-24
        /// </summary>
        public string? FechaFinVigencia { get; set; }

        /// <summary>
        /// Archivo del documento (multipart/form-data).
        /// </summary>
        public IFormFile File { get; set; } = default!;
    }
}