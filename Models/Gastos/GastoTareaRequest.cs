namespace velios.Api.Models.Tareas.Requests;

public class GastoTareaRequest
{
    public int IdTarea { get; set; }
    public decimal Gasto { get; set; }
    public string? Descripcion { get; set; }       // ← NUEVO
    public int? RegisteredById { get; set; }       // ← NUEVO
    public string? RegisteredByType { get; set; }  // ← NUEVO

}