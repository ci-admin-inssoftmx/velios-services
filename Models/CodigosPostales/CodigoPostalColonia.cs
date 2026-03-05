using Microsoft.EntityFrameworkCore;

namespace velios.Api.Models.CodigosPostales;

/// <summary>
/// Modelo sin llave (Keyless) para mapear consultas SQL de colonias.
/// </summary>
[Keyless]
public class CodigoPostalColonia
{
    public string d_asenta { get; set; } = "";
}