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
                (TareaId, ServicioId, FechaRegistro)
            VALUES
                (@TareaId, @ServicioId, GETDATE());
            SELECT SCOPE_IDENTITY();";

            using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                request.TareaId,
                request.ServicioId
            });
        }

        public async Task<bool> EditarSolicitudAsync(EditarSolicitudRequest request)
        {
            const string validarServicio = @"
                SELECT COUNT(1)
                FROM tb_CatServicios
                WHERE ServicioId = @ServicioId";

            const string existeSolicitud = @"
                SELECT COUNT(1)
                FROM tb_SolicitudServicios
                WHERE TareaId = @TareaId";

            const string insertar = @"
                INSERT INTO tb_SolicitudServicios
                 (TareaId, ServicioId, FechaRegistro)
                 VALUES
                (@TareaId, @ServicioId, GETDATE())";


            const string actualizar = @"
                UPDATE tb_SolicitudServicios
                SET ServicioId = @ServicioId
                WHERE TareaId = @TareaId";

            using var connection = new SqlConnection(_connectionString);

            var servicioExiste = await connection.ExecuteScalarAsync<int>(
                validarServicio, new { request.ServicioId });

            if (servicioExiste == 0)
                return false;

            var solicitudExiste = await connection.ExecuteScalarAsync<int>(
                existeSolicitud, new { request.TareaId });

            if (solicitudExiste > 0)
            {
                var filas = await connection.ExecuteAsync(actualizar, new
                {
                    request.TareaId,
                    request.ServicioId
                });
                return filas > 0;
            }
            else
            {
                var filas = await connection.ExecuteAsync(insertar, new
                {
                    request.TareaId,
                    request.ServicioId
                });
                return filas > 0;
            }
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
        public async Task<BuscadorJerarquiaResultado> BuscarJerarquiaAsync(string busqueda)
        {
            // Trae todo lo que tenga coincidencia en cualquier nivel
            // La lógica de agrupación se hace en C#
            const string sqlCategorias = @"
        SELECT DISTINCT
            cat.CategoriaServicioId,
            cat.CategoriaServicio,
            cat.Descripcion
        FROM tb_CatCategoriaServicios cat
        LEFT JOIN tb_CatSubcategoriaServicios sub
            ON sub.CategoriaServicioId = cat.CategoriaServicioId
        LEFT JOIN tb_CatServicios srv
            ON srv.SubcategoriaServicioId = sub.SubcategoriaServicioId
        WHERE cat.CategoriaServicio LIKE '%' + @Busqueda + '%'
           OR cat.Descripcion       LIKE '%' + @Busqueda + '%'
           OR sub.SubcategoriaServicio LIKE '%' + @Busqueda + '%'
           OR sub.Descripcion          LIKE '%' + @Busqueda + '%'
           OR srv.Servicio             LIKE '%' + @Busqueda + '%'
        ORDER BY cat.CategoriaServicio";

            const string sqlSubcategorias = @"
        SELECT DISTINCT
            sub.SubcategoriaServicioId,
            sub.SubcategoriaServicio,
            sub.Descripcion,
            sub.CategoriaServicioId
        FROM tb_CatSubcategoriaServicios sub
        LEFT JOIN tb_CatServicios srv
            ON srv.SubcategoriaServicioId = sub.SubcategoriaServicioId
        INNER JOIN tb_CatCategoriaServicios cat
            ON cat.CategoriaServicioId = sub.CategoriaServicioId
        WHERE cat.CategoriaServicio LIKE '%' + @Busqueda + '%'
           OR cat.Descripcion       LIKE '%' + @Busqueda + '%'
           OR sub.SubcategoriaServicio LIKE '%' + @Busqueda + '%'
           OR sub.Descripcion          LIKE '%' + @Busqueda + '%'
           OR srv.Servicio             LIKE '%' + @Busqueda + '%'
        ORDER BY sub.SubcategoriaServicio";

            const string sqlServicios = @"
        SELECT DISTINCT
            srv.ServicioId,
            srv.Servicio,
            srv.SubcategoriaServicioId
        FROM tb_CatServicios srv
        INNER JOIN tb_CatSubcategoriaServicios sub
            ON sub.SubcategoriaServicioId = srv.SubcategoriaServicioId
        INNER JOIN tb_CatCategoriaServicios cat
            ON cat.CategoriaServicioId = sub.CategoriaServicioId
        WHERE cat.CategoriaServicio LIKE '%' + @Busqueda + '%'
           OR cat.Descripcion       LIKE '%' + @Busqueda + '%'
           OR sub.SubcategoriaServicio LIKE '%' + @Busqueda + '%'
           OR sub.Descripcion          LIKE '%' + @Busqueda + '%'
           OR srv.Servicio             LIKE '%' + @Busqueda + '%'
        ORDER BY srv.Servicio";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var categorias = (await connection.QueryAsync(sqlCategorias, new { Busqueda = busqueda })).ToList();
            var subcategorias = (await connection.QueryAsync(sqlSubcategorias, new { Busqueda = busqueda })).ToList();
            var servicios = (await connection.QueryAsync(sqlServicios, new { Busqueda = busqueda })).ToList();

            // Armar jerarquía en C#
            var jerarquia = categorias.Select(cat => new CategoriaJerarquiaItem
            {
                CategoriaId = (int)cat.CategoriaServicioId,
                Categoria = (string)cat.CategoriaServicio,
                Descripcion = (string?)cat.Descripcion,
                Subcategorias = subcategorias
                    .Where(sub => (int)sub.CategoriaServicioId == (int)cat.CategoriaServicioId)
                    .Select(sub => new SubcategoriaJerarquiaItem
                    {
                        SubcategoriaId = (int)sub.SubcategoriaServicioId,
                        Subcategoria = (string)sub.SubcategoriaServicio,
                        Descripcion = (string?)sub.Descripcion,
                        Servicios = servicios
                            .Where(srv => (int)srv.SubcategoriaServicioId == (int)sub.SubcategoriaServicioId)
                            .Select(srv => new ServicioJerarquiaItem
                            {
                                ServicioId = (int)srv.ServicioId,
                                Servicio = (string)srv.Servicio
                            }).ToList()
                    }).ToList()
            }).ToList();

            return new BuscadorJerarquiaResultado { Jerarquia = jerarquia };
        }
        // ============================================================
        // BUSCADOR
        // ============================================================

        public async Task<BuscadorServicioResultado> BuscarAsync(string busqueda)
        {
            const string sql = @"
                -- Categorías
                SELECT
                    CategoriaServicioId,
                    CategoriaServicio,
                    Descripcion
                FROM tb_CatCategoriaServicios
                WHERE CategoriaServicio LIKE '%' + @Busqueda + '%'
                   OR Descripcion       LIKE '%' + @Busqueda + '%'
                ORDER BY CategoriaServicio;

                -- Subcategorías
                SELECT
                    sub.SubcategoriaServicioId,
                    sub.SubcategoriaServicio,
                    sub.Descripcion,
                    cat.CategoriaServicioId,
                    cat.CategoriaServicio
                FROM tb_CatSubcategoriaServicios sub
                INNER JOIN tb_CatCategoriaServicios cat
                    ON cat.CategoriaServicioId = sub.CategoriaServicioId
                WHERE sub.SubcategoriaServicio LIKE '%' + @Busqueda + '%'
                   OR sub.Descripcion          LIKE '%' + @Busqueda + '%'
                ORDER BY cat.CategoriaServicio, sub.SubcategoriaServicio;

                -- Servicios
                SELECT
                    srv.ServicioId,
                    srv.Servicio,
                    sub.SubcategoriaServicioId,
                    sub.SubcategoriaServicio,
                    cat.CategoriaServicioId,
                    cat.CategoriaServicio
                FROM tb_CatServicios srv
                INNER JOIN tb_CatSubcategoriaServicios sub
                    ON sub.SubcategoriaServicioId = srv.SubcategoriaServicioId
                INNER JOIN tb_CatCategoriaServicios cat
                    ON cat.CategoriaServicioId = sub.CategoriaServicioId
                WHERE srv.Servicio LIKE '%' + @Busqueda + '%'
                ORDER BY cat.CategoriaServicio, sub.SubcategoriaServicio, srv.Servicio;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var multi = await connection.QueryMultipleAsync(sql, new { Busqueda = busqueda });

            return new BuscadorServicioResultado
            {
                Categorias = await multi.ReadAsync<BuscadorCategoriaItem>(),
                Subcategorias = await multi.ReadAsync<BuscadorSubcategoriaItem>(),
                Servicios = await multi.ReadAsync<BuscadorServicioItem>()
            };
        }
    }
}