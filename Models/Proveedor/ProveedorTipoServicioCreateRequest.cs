using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Proveedores;

public class ProveedorTipoServicioCreateRequest
{
    [Required]
    public int TipoServicioId { get; set; }
}