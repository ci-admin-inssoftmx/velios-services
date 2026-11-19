using System.IO;
using System.Threading.Tasks;

namespace velios.Api.Services
{
    /// <summary>
    /// Abstracción de almacenamiento del archivo final del reporte.
    /// Empezamos con implementación local (Fase 1); si más adelante se
    /// necesita S3, solo se agrega otra implementación y se cambia el
    /// binding en Program.cs — nada del resto del flujo cambia.
    /// </summary>
    public interface IAlmacenamientoService
    {
        /// <summary>Guarda el contenido y devuelve la URL/ruta pública de descarga.</summary>
        Task<string> Guardar(Stream contenido, string nombreArchivo);
    }
}