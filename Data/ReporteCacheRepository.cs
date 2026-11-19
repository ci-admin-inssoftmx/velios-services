using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using velios.Api.Models;

namespace velios.Api.Data
{
    /// <summary>
    /// Acceso a tb_ReporteCache vía Dapper, siguiendo el mismo patrón usado
    /// en TareaRutaRepository: connection string desde IConfiguration,
    /// una SqlConnection nueva por método (using).
    /// </summary>
    public class ReporteCacheRepository : IReporteCacheRepository
    {
        private readonly string _connectionString;

        public ReporteCacheRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("VeliosConnection");
        }

        public async Task<ReporteCacheEntity> ObtenerVigentePorClave(string claveHash)
        {
            const string sql = @"
                SELECT TOP 1 *
                FROM tb_ReporteCache
                WHERE ClaveHash = @ClaveHash
                  AND Estado IN ('Procesando','Completado')
                ORDER BY FechaSolicitud DESC";

            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryFirstOrDefaultAsync<ReporteCacheEntity>(sql, new { ClaveHash = claveHash });
        }

        public async Task<ReporteCacheEntity> ObtenerPorJobId(string jobId)
        {
            const string sql = @"
                SELECT TOP 1 *
                FROM tb_ReporteCache
                WHERE HangfireJobId = @JobId";

            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryFirstOrDefaultAsync<ReporteCacheEntity>(sql, new { JobId = jobId });
        }

        public async Task<long> Crear(ReporteCacheEntity entidad)
        {
            const string sql = @"
                INSERT INTO tb_ReporteCache
                    (ClaveHash, TipoReporte, FiltrosJson, ProveedorId, ClienteId,
                     UsuarioSolicitanteId, Estado, HangfireJobId, FechaSolicitud)
                OUTPUT INSERTED.Id
                VALUES
                    (@ClaveHash, @TipoReporte, @FiltrosJson, @ProveedorId, @ClienteId,
                     @UsuarioSolicitanteId, @Estado, @HangfireJobId, @FechaSolicitud)";

            entidad.Estado ??= ReporteCacheEstados.Procesando;
            entidad.FechaSolicitud = entidad.FechaSolicitud == default ? DateTime.UtcNow : entidad.FechaSolicitud;

            using var conn = new SqlConnection(_connectionString);
            return await conn.ExecuteScalarAsync<long>(sql, entidad);
        }

        public async Task MarcarCompletado(string claveHash, string urlDescarga, string rutaAlmacenamiento, DateTime fechaExpiracion)
        {
            const string sql = @"
                UPDATE tb_ReporteCache
                SET Estado = 'Completado',
                    UrlDescarga = @UrlDescarga,
                    RutaAlmacenamiento = @RutaAlmacenamiento,
                    FechaCompletado = SYSUTCDATETIME(),
                    FechaExpiracion = @FechaExpiracion
                WHERE ClaveHash = @ClaveHash
                  AND Estado = 'Procesando'";

            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(sql, new { ClaveHash = claveHash, UrlDescarga = urlDescarga, RutaAlmacenamiento = rutaAlmacenamiento, FechaExpiracion = fechaExpiracion });
        }

        public async Task MarcarError(string claveHash, string mensajeError)
        {
            const string sql = @"
                UPDATE tb_ReporteCache
                SET Estado = 'Error',
                    MensajeError = @MensajeError,
                    FechaCompletado = SYSUTCDATETIME()
                WHERE ClaveHash = @ClaveHash
                  AND Estado = 'Procesando'";

            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(sql, new { ClaveHash = claveHash, MensajeError = mensajeError });
        }

        public async Task ActualizarProgreso(string claveHash, int totalRegistros, int registrosProcesados)
        {
            const string sql = @"
                UPDATE tb_ReporteCache
                SET TotalRegistros = @TotalRegistros,
                    RegistrosProcesados = @RegistrosProcesados
                WHERE ClaveHash = @ClaveHash
                  AND Estado = 'Procesando'";

            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(sql, new { ClaveHash = claveHash, TotalRegistros = totalRegistros, RegistrosProcesados = registrosProcesados });
        }

        public async Task<int> ExpirarVencidos()
        {
            const string sql = @"
                UPDATE tb_ReporteCache
                SET Estado = 'Expirado'
                WHERE Estado = 'Completado'
                  AND FechaExpiracion < SYSUTCDATETIME()";

            using var conn = new SqlConnection(_connectionString);
            return await conn.ExecuteAsync(sql);
        }
        public async Task MarcarExpirado(string claveHash)
        {
            const string sql = @"
        UPDATE tb_ReporteCache
        SET Estado = 'Expirado'
        WHERE ClaveHash = @ClaveHash
          AND Estado = 'Completado'";

            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(sql, new { ClaveHash = claveHash });
        }
    }

}