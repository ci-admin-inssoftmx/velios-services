using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace velios.Api.Services
{
    /// <summary>
    /// Guarda los reportes generados en disco (ej. wwwroot/reportes-generados),
    /// adecuado para el esquema actual de despliegue en IIS vía FTP.
    /// </summary>
    public class AlmacenamientoLocalService : IAlmacenamientoService
    {
        private readonly string _rutaBase;
        private readonly string _urlBase;

        public AlmacenamientoLocalService(IConfiguration configuration)
        {
            // Agregar a appsettings.json:
            // "ReportesAlmacenamiento": { "RutaBase": "wwwroot/reportes-generados", "UrlBase": "/reportes-generados" }
            _rutaBase = configuration["ReportesAlmacenamiento:RutaBase"] ?? "wwwroot/reportes-generados";
            _urlBase = configuration["ReportesAlmacenamiento:UrlBase"] ?? "/reportes-generados";

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

            return $"{_urlBase}/{nombreSeguro}";
        }

        private static string SanitizarNombreArchivo(string nombre)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre;
        }
    }
}