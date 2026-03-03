using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Proveedores;

public class ProyectoTrabajadorAssignRequest
{
    [Required] public long ProveedorProyectoId { get; set; }
    [Required] public int ProveedorId { get; set; }
    [Required] public long TrabajadorId { get; set; }
}