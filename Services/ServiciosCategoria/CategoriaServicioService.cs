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

        public async Task<IEnumerable<CategoriaServicioModel>> GetCategoriasAsync()
        {
            const string sql = @"
                SELECT 
                    CategoriaServicioId,
                    CategoriaServicio
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
                    SubcategoriaServicio
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
    }  // ← solo una llave de cierre, sin ";"

}