using System.Threading.Tasks;
using velios.Api.Models;

namespace velios.Api.Data
{
    public interface IReporteCacheRepository
    {
        /// <summary>Busca un registro vigente (Procesando o Completado) por clave hash.</summary>
        Task<ReporteCacheEntity> ObtenerVigentePorClave(string claveHash);

        Task<ReporteCacheEntity> ObtenerPorJobId(string jobId);

        Task<long> Crear(ReporteCacheEntity entidad);

        Task MarcarCompletado(string claveHash, string urlDescarga, string rutaAlmacenamiento, System.DateTime fechaExpiracion);

        Task MarcarError(string claveHash, string mensajeError);

        Task ActualizarProgreso(string claveHash, int totalRegistros, int registrosProcesados);

        /// <summary>Marca como Expirado todo lo vencido. Pensado para un job recurrente de limpieza.</summary>
        Task<int> ExpirarVencidos();
    }
}