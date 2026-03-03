using velios.Api.Models.CodigosPostales;

namespace velios.Api.Services.CodigosPostales;

public interface ICodigoPostalService
{
    Task<CodigoPostalInfoDto?> GetInfoAsync(string codigoPostal);
    Task<List<string>> SearchColoniasAsync(string codigoPostal, string? q, int take = 20);
}