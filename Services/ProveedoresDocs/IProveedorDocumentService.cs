namespace velios.Api.Services.ProveedoresDocs;

public interface IProveedorDocumentService
{
    Task<long> UploadAsync(int proveedorId, int tipoDocumentoId, IFormFile file, DateTime? fechaFinVigencia, string? actor);
    Task<(byte[] bytes, string contentType, string fileName)?> DownloadAsync(long proveedorDocumentoId);
    Task<List<object>> ListAsync(int proveedorId);
    Task<bool> DeleteAsync(long proveedorDocumentoId, string? actor);
}