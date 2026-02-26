using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Proveedores;

public class PresupuestoCreateRequest
{
    [Required]
    public long ProveedorProyectoId { get; set; }

    [Required]
    public int ProveedorId { get; set; }

    [Required]
    public decimal Monto { get; set; }

    public string? Moneda { get; set; } = "MXN";
    public string? Descripcion { get; set; }
}