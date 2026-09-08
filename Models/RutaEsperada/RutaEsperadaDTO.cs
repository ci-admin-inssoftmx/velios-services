public class ParadaRutaEsperadaDto
{
    public int? Id { get; set; }
    public int Orden { get; set; }
    public int TipoParada { get; set; } // 1=Inicio, 2=Parada, 3=Fin
    public string Direccion { get; set; } = string.Empty;
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
}

public class GuardarRutaEsperadaRequest
{
    public int TareaId { get; set; }
    public List<ParadaRutaEsperadaDto> Paradas { get; set; } = new();
}