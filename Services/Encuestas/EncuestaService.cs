using Dapper;
using Microsoft.Data.SqlClient;
using velios.Api.Models.Encuestas;

namespace velios.Api.Services.Encuestas
{
    public class EncuestaService : IEncuestaService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public EncuestaService(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("VeliosConnection")!;
        }

        // ============================================================
        // Servicio 1: Traer encuesta para llenar (con llenado previo)
        // ============================================================
        public async Task<EncuestaModel?> GetEncuestaAsync(int idServicio, int tareaId)
        {
            const string sql = @"
        SELECT
            e.EncuestaId,
            e.Titulo,
            e.Descripcion,
            p.PreguntaId,
            p.Orden,
            p.Texto         AS TextoPregunta,
            p.Tipo,
            p.Requerido,
            r.RespuestaId,
            r.Valor,
            r.Texto         AS TextoRespuesta,
            ru.RespuestaId  AS RespuestaUsuario
        FROM tb_CatEncuesta e
        INNER JOIN tb_CatPreguntas p  ON p.EncuestaId  = e.EncuestaId
        INNER JOIN tb_CatRespuestas r ON r.PreguntaId  = p.PreguntaId
        LEFT  JOIN tb_EncuestaRespuestaUsuario ru
               ON ru.PreguntaId = p.PreguntaId
              AND ru.TareaId    = @TareaId
        WHERE e.Activo = 1
          AND e.EncuestaId = (
                SELECT ISNULL(
                    (SELECT TOP 1 EncuestaId FROM tb_CatEncuesta WHERE IdServicio = @IdServicio AND Activo = 1),
                    1
                )
          )
        ORDER BY p.Orden, r.Valor";

            using var connection = new SqlConnection(_connectionString);
            return await MapearEncuesta(connection, sql, new { IdServicio = idServicio, TareaId = tareaId });
        }


        // ============================================================
        // Servicio 2: Guardar respuesta de usuario
        // ============================================================
        public async Task<bool> GuardarRespuestaAsync(GuardarRespuestaRequest request)
        {
            const string sql = @"
                IF EXISTS (
                    SELECT 1 FROM tb_EncuestaRespuestaUsuario
                    WHERE TareaId    = @TareaId
                      AND PreguntaId = @PreguntaId
                )
                    UPDATE tb_EncuestaRespuestaUsuario
                    SET RespuestaId    = @RespuestaId,
                        FechaRespuesta = GETDATE()
                    WHERE TareaId    = @TareaId
                      AND PreguntaId = @PreguntaId
                ELSE
                    INSERT INTO tb_EncuestaRespuestaUsuario
                        (TareaId, EncuestaId, PreguntaId, RespuestaId)
                    VALUES
                        (@TareaId, @EncuestaId, @PreguntaId, @RespuestaId)";

            using var connection = new SqlConnection(_connectionString);
            var filas = await connection.ExecuteAsync(sql, new
            {
                request.TareaId,
                request.EncuestaId,
                request.PreguntaId,
                request.RespuestaId
            });

            return filas > 0;
        }

        // ============================================================
        // Servicio 3: Traer encuesta respondida
        // ============================================================
        public async Task<EncuestaModel?> GetEncuestaRespondidaAsync(int encuestaId, int tareaId)
        {
            const string sql = @"
                SELECT
                    e.EncuestaId,
                    e.Titulo,
                    e.Descripcion,
                    p.PreguntaId,
                    p.Orden,
                    p.Texto         AS TextoPregunta,
                    p.Tipo,
                    p.Requerido,
                    r.RespuestaId,
                    r.Valor,
                    r.Texto         AS TextoRespuesta,
                    ru.RespuestaId  AS RespuestaUsuario
                FROM tb_CatEncuesta e
                INNER JOIN tb_CatPreguntas p  ON p.EncuestaId  = e.EncuestaId
                INNER JOIN tb_CatRespuestas r ON r.PreguntaId  = p.PreguntaId
                LEFT  JOIN tb_EncuestaRespuestaUsuario ru
                       ON ru.PreguntaId = p.PreguntaId
                      AND ru.TareaId    = @TareaId
                WHERE e.EncuestaId = @EncuestaId
                ORDER BY p.Orden, r.Valor";

            using var connection = new SqlConnection(_connectionString);
            return await MapearEncuesta(connection, sql, new { EncuestaId = encuestaId, TareaId = tareaId });
        }

        // ============================================================
        // Método privado: mapea el resultado a EncuestaModel
        // Reutilizado por Servicio 1 y Servicio 3
        // ============================================================
        private static async Task<EncuestaModel?> MapearEncuesta(
           SqlConnection connection, string sql, object parametros)
        {
            var rows = await connection.QueryAsync(sql, parametros);
            var list = rows.ToList();

            if (!list.Any()) return null;

            var primera = list.First();

            var encuesta = new EncuestaModel
            {
                EncuestaId = primera.EncuestaId,
                Titulo = primera.Titulo,
                Descripcion = primera.Descripcion
            };

            var preguntas = list
                .GroupBy(r => (int)r.PreguntaId)
                .Select(g =>
                {
                    var p = g.First();
                    return new PreguntaModel
                    {
                        PreguntaId = p.PreguntaId,
                        Orden = p.Orden,
                        Texto = p.TextoPregunta,
                        Tipo = p.Tipo,
                        Requerido = p.Requerido,
                        RespuestaUsuario = p.RespuestaUsuario,
                        Respuestas = g.Select(r => new RespuestaOpcionModel
                        {
                            RespuestaId = r.RespuestaId,
                            Valor = r.Valor,
                            Texto = r.TextoRespuesta
                        }).ToList()
                    };
                })
                .OrderBy(p => p.Orden)
                .ToList();

            encuesta.Preguntas = preguntas;

            var totalPreguntas = preguntas.Count;
            var preguntasRespondidas = preguntas.Count(p => p.RespuestaUsuario != null);
            encuesta.Completa = totalPreguntas == preguntasRespondidas ? 1 : 0;

            return encuesta;
        }
    }
    }