using Dapper;
using Microsoft.Data.SqlClient;
using velios.Api.Models.PresupuestoGuardado;

namespace velios.Api.Services.PresupuestoGuardado
{
    public class PresupuestoGuardadoService : IPresupuestoGuardadoService
    {
        private readonly string _connectionString;

        public PresupuestoGuardadoService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("VeliosConnection")!;
        }

        // ============================================================
        // Guardar presupuesto
        // ============================================================
        public async Task<int> GuardarPresupuestoAsync(GuardarPresupuestoRequest request)
        {
            const string sqlPresupuesto = @"
                SELECT PresupuestoAsignado
                FROM tb_Tareas
                WHERE TareaId = @TareaId
                  AND IsDeleted = 0";

            const string sqlInsert = @"
                INSERT INTO tb_PresupuestoGuardado
                    (TareaId, PresupuestoAsignado, Descripcion, PresupuestoDisponible, FechaLlenado)
                VALUES
                    (@TareaId, @PresupuestoAsignado, @Descripcion, @PresupuestoDisponible, GETDATE());

                SELECT SCOPE_IDENTITY();";

            using var connection = new SqlConnection(_connectionString);

            var presupuestoAsignado = await connection.ExecuteScalarAsync<decimal?>(
                sqlPresupuesto, new { request.TareaId });

            if (presupuestoAsignado == null)
                throw new KeyNotFoundException($"No se encontró la tarea {request.TareaId}.");

            return await connection.ExecuteScalarAsync<int>(sqlInsert, new
            {
                request.TareaId,
                PresupuestoAsignado = presupuestoAsignado.Value,
                Descripcion = request.Descripcion.Trim(),
                request.PresupuestoDisponible
            });
        }

        // ============================================================
        // Obtener presupuestos por tarea
        // ============================================================
        public async Task<IEnumerable<PresupuestoGuardadoModel>> GetPresupuestosByTareaAsync(int tareaId)
        {
            const string sql = @"
                SELECT
                    PresupuestoGuardadoId,
                    TareaId,
                    PresupuestoAsignado,
                    Descripcion,
                    PresupuestoDisponible,
                    FechaLlenado
                FROM tb_PresupuestoGuardado
                WHERE TareaId = @TareaId
                ORDER BY FechaLlenado DESC";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<PresupuestoGuardadoModel>(sql, new { TareaId = tareaId });
        }
    }
}