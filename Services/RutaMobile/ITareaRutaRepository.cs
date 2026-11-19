using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient;
using Dapper;

public interface ITareaRutaRepository
{
    Task<RutaDto?> ObtenerRutaActivaAsync(int tareaId);
    Task<int> InsertarRutaAsync(IniciarRutaRequest request);
    Task FinalizarRutaAsync(int rutaId, FinalizarRutaRequest request);
    Task<RutaDto?> ObtenerRutaPorIdAsync(int rutaId);
    Task<List<EvidenciaGeoDto>> ObtenerEvidenciasPorRutaAsync(int rutaId);
    Task<bool> RutaEstaActivaAsync(int rutaId, int tareaId);
    Task<List<int>> ObtenerTareaIdsConRutaActivaAsync(IEnumerable<int> tareaIds);
    Task<RutaDto?> ObtenerUltimaRutaPorTareaAsync(int tareaId); // ← NUEVO
}

public class TareaRutaRepository : ITareaRutaRepository
{
    private readonly string _connectionString;

    public TareaRutaRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("VeliosConnection");
    }

    public async Task<RutaDto?> ObtenerRutaActivaAsync(int tareaId)
    {
        const string sql = @"
            SELECT TOP 1 Id, TareaId, Estado, LatitudInicio, LongitudInicio, FechaHoraInicio,
                   LatitudFin, LongitudFin, FechaHoraFin
            FROM tb_TareaRuta
            WHERE TareaId = @TareaId AND Estado = 1
            ORDER BY Id DESC";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<RutaDto>(sql, new { TareaId = tareaId });
    }

    public async Task<int> InsertarRutaAsync(IniciarRutaRequest request)
    {
        const string sql = @"
            INSERT INTO tb_TareaRuta (TareaId, Estado, LatitudInicio, LongitudInicio, FechaHoraInicio, UsuarioId, FechaCreacion)
            OUTPUT INSERTED.Id
            VALUES (@TareaId, 1, @Latitud, @Longitud, GETDATE(), @UsuarioId, GETDATE())";

        using var conn = new SqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(sql, request);
    }

    public async Task FinalizarRutaAsync(int rutaId, FinalizarRutaRequest request)
    {
        const string sql = @"
            UPDATE tb_TareaRuta
            SET Estado = 2, LatitudFin = @Latitud, LongitudFin = @Longitud, FechaHoraFin = GETDATE()
            WHERE Id = @RutaId AND Estado = 1";

        using var conn = new SqlConnection(_connectionString);
        var rows = await conn.ExecuteAsync(sql, new { RutaId = rutaId, request.Latitud, request.Longitud });

        if (rows == 0)
            throw new InvalidOperationException("La ruta no existe o ya fue finalizada.");
    }

    public async Task<RutaDto?> ObtenerRutaPorIdAsync(int rutaId)
    {
        const string sql = @"
            SELECT Id, TareaId, Estado, LatitudInicio, LongitudInicio, FechaHoraInicio,
                   LatitudFin, LongitudFin, FechaHoraFin
            FROM tb_TareaRuta
            WHERE Id = @RutaId";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<RutaDto>(sql, new { RutaId = rutaId });
    }

    public async Task<List<EvidenciaGeoDto>> ObtenerEvidenciasPorRutaAsync(int rutaId)
    {
        const string sql = @"
        SELECT EvidenciaId, Latitud, Longitud, DateCreated, UrlArchivo
        FROM tb_TareaEvidencias
        WHERE RutaId = @RutaId
        ORDER BY DateCreated ASC";

        using var conn = new SqlConnection(_connectionString);
        var result = await conn.QueryAsync<EvidenciaGeoDto>(sql, new { RutaId = rutaId });
        return result.ToList();
    }
    public async Task<bool> RutaEstaActivaAsync(int rutaId, int tareaId)
    {
        const string sql = @"
        SELECT COUNT(1)
        FROM tb_TareaRuta
        WHERE Id = @RutaId AND TareaId = @TareaId AND Estado = 1";

        using var conn = new SqlConnection(_connectionString);
        var count = await conn.ExecuteScalarAsync<int>(sql, new { RutaId = rutaId, TareaId = tareaId });
        return count > 0;
    }
    public async Task<List<int>> ObtenerTareaIdsConRutaActivaAsync(IEnumerable<int> tareaIds)
    {
        const string sql = @"
        SELECT DISTINCT TareaId
        FROM tb_TareaRuta
        WHERE TareaId IN @TareaIds AND Estado = 1";

        using var conn = new SqlConnection(_connectionString);
        var result = await conn.QueryAsync<int>(sql, new { TareaIds = tareaIds });
        return result.ToList();
    }
    public async Task<RutaDto?> ObtenerUltimaRutaPorTareaAsync(int tareaId)
    {
        const string sql = @"
            SELECT TOP 1 Id, TareaId, Estado, LatitudInicio, LongitudInicio, FechaHoraInicio,
                   LatitudFin, LongitudFin, FechaHoraFin
            FROM tb_TareaRuta
            WHERE TareaId = @TareaId
            ORDER BY Id DESC";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<RutaDto>(sql, new { TareaId = tareaId });
    }
}