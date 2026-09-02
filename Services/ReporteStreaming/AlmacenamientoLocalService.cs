using velios.Api.Services;

public class AlmacenamientoLocalService : IAlmacenamientoService
{
    private readonly string _rutaBase;
    private readonly string _urlBase;
    private readonly string _publicBaseUrl; // NUEVO

    public AlmacenamientoLocalService(IConfiguration configuration)
    {
        _rutaBase = configuration["ReportesAlmacenamiento:RutaBase"] ?? "wwwroot/reportes-generados";
        _urlBase = configuration["ReportesAlmacenamiento:UrlBase"] ?? "/reportes-generados";
        _publicBaseUrl = configuration["ReportesAlmacenamiento:PublicBaseUrl"]?.TrimEnd('/'); // NUEVO

        Directory.CreateDirectory(_rutaBase);
    }

    public async Task<string> Guardar(Stream contenido, string nombreArchivo)
    {
        var nombreSeguro = SanitizarNombreArchivo(nombreArchivo);
        var rutaCompleta = Path.Combine(_rutaBase, nombreSeguro);

        await using (var fs = File.Create(rutaCompleta))
        {
            if (contenido.CanSeek) contenido.Position = 0;
            await contenido.CopyToAsync(fs);
        }

        var rutaRelativa = $"{_urlBase}/{nombreSeguro}";

        // NUEVO: si hay PublicBaseUrl configurado, arma la URL absoluta.
        // Si no está configurado, cae al comportamiento anterior (relativa) sin romper nada.
        return string.IsNullOrEmpty(_publicBaseUrl)
            ? rutaRelativa
            : $"{_publicBaseUrl}{rutaRelativa}";
    }

    private static string SanitizarNombreArchivo(string nombre)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            nombre = nombre.Replace(c, '_');
        return nombre;
    }
}