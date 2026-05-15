namespace velios.Api.Models.Encuestas
{
    // ============================================================
    // Respuesta del Servicio 1 y 3: Encuesta completa
    // ============================================================
    public class EncuestaModel
    {
        public int EncuestaId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Completa { get; set; } // 1 = completa, 0 = pendiente
        public List<PreguntaModel> Preguntas { get; set; } = new();
    }

    public class PreguntaModel
    {
        public int PreguntaId { get; set; }
        public int Orden { get; set; }
        public string Texto { get; set; } = string.Empty;
        public string? Tipo { get; set; }
        public bool Requerido { get; set; }
        public int? RespuestaUsuario { get; set; } // null si no ha respondido
        public List<RespuestaOpcionModel> Respuestas { get; set; } = new();
    }

    public class RespuestaOpcionModel
    {
        public int RespuestaId { get; set; }
        public int Valor { get; set; }
        public string Texto { get; set; } = string.Empty;
    }

    // ============================================================
    // Request del Servicio 2: Guardar respuesta de usuario
    // ============================================================
    public class GuardarRespuestaRequest
    {
        public int TareaId { get; set; }
        public int EncuestaId { get; set; }
        public int PreguntaId { get; set; }
        public int RespuestaId { get; set; }
    }
}