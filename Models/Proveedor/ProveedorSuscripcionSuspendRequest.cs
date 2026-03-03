using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Proveedores;

public class ProveedorSuscripcionSuspendRequest
{
    [MaxLength(250)]
    public string? Motivo { get; set; }
}