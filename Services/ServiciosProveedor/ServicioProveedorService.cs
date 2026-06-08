using Dapper;
using Microsoft.Data.SqlClient;
using velios.Api.Models.ServiciosProveedor;

namespace velios.Api.Services.ServiciosProveedor
{
    public class ServicioProveedorService : IServicioProveedorService
    {
        private readonly string _connectionString;

        public ServicioProveedorService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("VeliosConnection")!;
        }

        // ============================================================
        // Asignar múltiples servicios a un proveedor
        // ============================================================
        public async Task<AsignarServiciosResultado> AsignarServiciosAsync(AsignarServiciosProveedorRequest request)
        {
            // Obtiene CategoriaServicioId y SubcategoriaServicioId desde tb_CatServicios
            const string sqlCatalogo = @"
                SELECT
                    s.ServicioId,
                    s.SubcategoriaServicioId,
                    sub.CategoriaServicioId
                FROM tb_CatServicios s
                INNER JOIN tb_CatSubcategoriaServicios sub
                    ON sub.SubcategoriaServicioId = s.SubcategoriaServicioId
                WHERE s.ServicioId IN @ServicioIds";

            const string sqlDuplicado = @"
                SELECT ServicioId
                FROM tb_ServiciosProveedores
                WHERE ProveedorId = @ProveedorId
                  AND ServicioId IN @ServicioIds";

            const string sqlInsert = @"
                INSERT INTO tb_ServiciosProveedores
                    (ProveedorId, CategoriaServicioId, SubcategoriaServicioId, ServicioId)
                VALUES
                    (@ProveedorId, @CategoriaServicioId, @SubcategoriaServicioId, @ServicioId)";

            using var connection = new SqlConnection(_connectionString);

            // 1. Leer catálogo para obtener categoría y subcategoría de cada servicio
            var catalogo = (await connection.QueryAsync(sqlCatalogo, new { request.ServicioIds }))
                .ToDictionary(r => (int)r.ServicioId, r => r);

            // 2. Detectar duplicados ya existentes en la tabla
            var duplicados = (await connection.QueryAsync<int>(sqlDuplicado, new
            {
                request.ProveedorId,
                request.ServicioIds
            })).ToHashSet();

            var resultado = new AsignarServiciosResultado
            {
                ServiciosDuplicados = duplicados.ToList(),
                Duplicados = duplicados.Count
            };

            // 3. Insertar solo los que no están duplicados y existen en catálogo
            var aInsertar = request.ServicioIds
                .Where(id => !duplicados.Contains(id) && catalogo.ContainsKey(id))
                .ToList();

            foreach (var servicioId in aInsertar)
            {
                var cat = catalogo[servicioId];
                await connection.ExecuteAsync(sqlInsert, new
                {
                    request.ProveedorId,
                    CategoriaServicioId = (int)cat.CategoriaServicioId,
                    SubcategoriaServicioId = (int)cat.SubcategoriaServicioId,
                    ServicioId = servicioId
                });
                resultado.Insertados++;
            }

            return resultado;
        }
        public async Task<IEnumerable<ProveedorPorServicioModel>> GetProveedoresByServicioAsync(int servicioId)
        {
            const string sql = @"
        SELECT
            p.ProveedorId,
            p.NombreComercial,
            p.CorreoContacto
        FROM tb_ServiciosProveedores sp
        INNER JOIN tb_Proveedores p
            ON p.ProveedorId = sp.ProveedorId
        WHERE sp.ServicioId = @ServicioId
        ORDER BY p.NombreComercial";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<ProveedorPorServicioModel>(sql, new { ServicioId = servicioId });
        }

        // ============================================================
        // Obtener todos los servicios asignados a un proveedor
        // ============================================================
        public async Task<IEnumerable<ServicioProveedorModel>> GetServiciosByProveedorAsync(int proveedorId)
        {
            const string sql = @"
                SELECT
                    sp.ProveedorServicioId,
                    sp.ProveedorId,
                    sp.CategoriaServicioId,
                    cat.CategoriaServicio,
                    sp.SubcategoriaServicioId,
                    sub.SubcategoriaServicio,
                    sp.ServicioId,
                    srv.Servicio,
                    sp.FechaRegistro
                FROM tb_ServiciosProveedores sp
                INNER JOIN tb_CatServicios srv
                    ON srv.ServicioId = sp.ServicioId
                INNER JOIN tb_CatSubcategoriaServicios sub
                    ON sub.SubcategoriaServicioId = sp.SubcategoriaServicioId
                INNER JOIN tb_CatCategoriaServicios cat
                    ON cat.CategoriaServicioId = sp.CategoriaServicioId
                WHERE sp.ProveedorId = @ProveedorId
                ORDER BY cat.CategoriaServicio, sub.SubcategoriaServicio, srv.Servicio";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<ServicioProveedorModel>(sql, new { ProveedorId = proveedorId });
        }
    }
}