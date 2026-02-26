using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Proveedores;

public class ProveedorSuscripcionUpsertRequest
{
    [Required]
    public int PaqueteId { get; set; }

    public DateTime? FechaInicio { get; set; } // si no viene, hoy
}