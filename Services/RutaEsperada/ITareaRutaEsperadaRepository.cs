using Microsoft.Data.SqlClient;
using Dapper;

public interface ITareaRutaEsperadaRepository
{
    Task<List<ParadaRutaEsperadaDto>> ObtenerParadasAsync(int tareaId);
    Task GuardarParadasAsync(int tareaId, List<ParadaRutaEsperadaDto> paradas);
}

public class TareaRutaEsperadaRepository : ITareaRutaEsperadaRepository
{
    private readonly string _connectionString;

    public TareaRutaEsperadaRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("VeliosConnection");
    }

    public async Task<List<ParadaRutaEsperadaDto>> ObtenerParadasAsync(int tareaId)
    {
        const string sql = @"
            SELECT Id, Orden, TipoParada, Direccion, Latitud, Longitud
            FROM tb_TareaRutaEsperada
            WHERE TareaId = @TareaId
            ORDER BY Orden ASC";

        using var conn = new SqlConnection(_connectionString);
        var result = await conn.QueryAsync<ParadaRutaEsperadaDto>(sql, new { TareaId = tareaId });
        return result.ToList();
    }

    public async Task GuardarParadasAsync(int tareaId, List<ParadaRutaEsperadaDto> paradas)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var tran = conn.BeginTransaction();

        try
        {
            // Reemplaza todo el set de paradas (simplifica el guardado desde el form)
            await conn.ExecuteAsync(
                "DELETE FROM tb_TareaRutaEsperada WHERE TareaId = @TareaId",
                new { TareaId = tareaId }, tran);

            const string insertSql = @"
                INSERT INTO tb_TareaRutaEsperada (TareaId, Orden, TipoParada, Direccion, Latitud, Longitud, FechaCreacion)
                VALUES (@TareaId, @Orden, @TipoParada, @Direccion, @Latitud, @Longitud, GETDATE())";

            foreach (var parada in paradas)
            {
                await conn.ExecuteAsync(insertSql, new
                {
                    TareaId = tareaId,
                    parada.Orden,
                    parada.TipoParada,
                    parada.Direccion,
                    parada.Latitud,
                    parada.Longitud
                }, tran);
            }

            tran.Commit();
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }
}