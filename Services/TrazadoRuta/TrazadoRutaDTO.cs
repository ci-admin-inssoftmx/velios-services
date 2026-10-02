namespace velios.Api.Services.TrazadoRuta
{
    /// <summary>
    /// Respuesta del endpoint GET api/rutas/{rutaId}/trazado.
    /// Las polilíneas viajan codificadas (formato Google) para que el payload sea pequeño;
    /// la web las decodifica y las usa tanto en Static Maps (path=enc:...) como en el SVG.
    /// </summary>
    public class TrazadoRutaDto
    {
        public int RutaId { get; set; }

        /// <summary>Recorrido real por calles (inicio → evidencias → fin). Null si no se pudo calcular.</summary>
        public string? RecorridoPolilinea { get; set; }
        public int? RecorridoDistanciaMetros { get; set; }

        /// <summary>Ruta esperada por calles (paradas ordenadas). Null si no se pudo calcular.</summary>
        public string? EsperadaPolilinea { get; set; }
        public int? EsperadaDistanciaMetros { get; set; }

        /// <summary>Mensajes para mostrar al usuario cuando algún cálculo no fue posible.</summary>
        public List<string> Avisos { get; set; } = new();
    }

    /// <summary>Resultado de calcular una ruta contra Google (una polilínea ya unida si hubo varios tramos).</summary>
    public class RutaCalculadaDto
    {
        public string Polilinea { get; set; } = string.Empty;
        public int DistanciaMetros { get; set; }
    }

    /// <summary>Fila de tb_TareaRutaTrazadoCache.</summary>
    public class TrazadoCacheEntry
    {
        public string HuellaPuntos { get; set; } = string.Empty;
        public string Polilinea { get; set; } = string.Empty;
        public int? DistanciaMetros { get; set; }
    }
}