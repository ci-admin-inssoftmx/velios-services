using Dapper;
using Microsoft.Data.SqlClient;
using velios.Api.Models.ServiciosCategoria;

namespace velios.Api.Services.ServiciosCategoria
{
    public class CategoriaServicioService : ICategoriaServicioService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public CategoriaServicioService(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("VeliosConnection")!;
        }

        // ============================================================
        // CATÁLOGOS
        // ============================================================

        public async Task<IEnumerable<CategoriaServicioModel>> GetCategoriasAsync()
        {
            const string sql = @"
                SELECT 
                    CategoriaServicioId,
                    CategoriaServicio,
                    Descripcion        
                FROM tb_CatCategoriaServicios
                ORDER BY CategoriaServicio";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<CategoriaServicioModel>(sql);
        }

        public async Task<IEnumerable<SubcategoriaServicioModel>> GetSubcategoriasByCategoriaAsync(int categoriaId)
        {
            const string sql = @"
                SELECT 
                    SubcategoriaServicioId,
                    SubcategoriaServicio,
                    Descripcion        
                FROM tb_CatSubcategoriaServicios
                WHERE CategoriaServicioId = @CategoriaId
                ORDER BY SubcategoriaServicio";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<SubcategoriaServicioModel>(sql, new { CategoriaId = categoriaId });
        }

        public async Task<IEnumerable<ServicioModel>> GetServiciosBySubcategoriaAsync(int subcategoriaId)
        {
            const string sql = @"
                SELECT 
                    ServicioId,
                    Servicio
                FROM tb_CatServicios
                WHERE SubcategoriaServicioId = @SubcategoriaId
                ORDER BY Servicio";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<ServicioModel>(sql, new { SubcategoriaId = subcategoriaId });
        }

        // ============================================================
        // SOLICITUD
        // ============================================================

        public async Task<int> GuardarSolicitudAsync(GuardarSolicitudRequest request)
        {
            const string sql = @"
                INSERT INTO tb_SolicitudServicios
                    (TareaId, ServicioId, ClienteId)
                VALUES
                    (@TareaId, @ServicioId, @ClienteId);

                SELECT SCOPE_IDENTITY();";

            using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                request.TareaId,
                request.ServicioId,
                request.ClienteId
            });
        }

public async Task<bool> EditarSolicitudAsync(EditarSolicitudRequest request)
{
    const string validarTarea = @"
        SELECT COUNT(1)
        FROM tb_SolicitudServicios
        WHERE TareaId = @TareaId";

    const string validarServicio = @"
        SELECT COUNT(1)
        FROM tb_CatServicios
        WHERE ServicioId = @ServicioId";

    const string actualizar = @"
        UPDATE tb_SolicitudServicios
        SET ServicioId = @ServicioId
        WHERE TareaId = @TareaId";

    using var connection = new SqlConnection(_connectionString);

    // Validar tarea
    var existeTarea = await connection.ExecuteScalarAsync<int>(
        validarTarea,
        new { request.TareaId });

    if (existeTarea == 0)
        return false;

    // Validar servicio
    var existeServicio = await connection.ExecuteScalarAsync<int>(
        validarServicio,
        new { request.ServicioId });

    if (existeServicio == 0)
        return false;

    // Actualizar
    var filas = await connection.ExecuteAsync(actualizar, new
    {
        request.TareaId,
        request.ServicioId
    });

    return filas > 0;
}

        public async Task<SolicitudServicioModel?> GetSolicitudAsync(int tareaId)
        {
            const string sql = @"
                SELECT
                    ss.SolicitudId,
                    ss.TareaId,
                    ss.ClienteId,
                    cat.CategoriaServicioId,
                    cat.CategoriaServicio,
                    sub.SubcategoriaServicioId,
                    sub.SubcategoriaServicio,
                    srv.ServicioId,
                    srv.Servicio,
                    ss.FechaRegistro
                FROM tb_SolicitudServicios ss
                INNER JOIN tb_CatServicios srv
                    ON srv.ServicioId = ss.ServicioId
                INNER JOIN tb_CatSubcategoriaServicios sub
                    ON sub.SubcategoriaServicioId = srv.SubcategoriaServicioId
                INNER JOIN tb_CatCategoriaServicios cat
                    ON cat.CategoriaServicioId = sub.CategoriaServicioId
                WHERE ss.TareaId = @TareaId";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<SolicitudServicioModel>(sql, new { TareaId = tareaId });
        }
    }
}