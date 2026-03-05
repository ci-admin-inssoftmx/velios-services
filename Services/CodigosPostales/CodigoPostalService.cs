using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.CodigosPostales;

namespace velios.Api.Services.CodigosPostales;

/// <summary>
/// Servicio de consulta para catálogo SEPOMEX.
/// </summary>
public class CodigoPostalService : ICodigoPostalService
{
    private readonly AppDbContext _db;

    public CodigoPostalService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Devuelve Estado, Municipio y lista de colonias para un CP.
    /// </summary>
    public async Task<CodigoPostalInfoDto?> GetInfoAsync(string codigoPostal)
    {
        var cp = Normalize(codigoPostal);
        if (cp == null) return null;

        var rows = await _db.CodigosPostales
            .AsNoTracking()
            .Where(x => x.d_codigo == cp && !x.IsDeleted)
            .ToListAsync();

        if (rows.Count == 0) return null;

        return new CodigoPostalInfoDto
        {
            CodigoPostal = cp,
            Estado = rows[0].d_estado,
            Municipio = rows[0].D_mnpio,
            Colonias = rows.Select(x => x.d_asenta.Trim())
                           .Distinct()
                           .OrderBy(x => x)
                           .ToList()
        };
    }

    /// <summary>
    /// Busca colonias dentro de un CP (autocomplete).
    /// </summary>
    public async Task<List<string>> SearchColoniasAsync(string codigoPostal, string? q, int take = 20)
    {
        var cp = Normalize(codigoPostal);
        if (cp == null) return new();

        var query = _db.CodigoPostalColonias
            .FromSqlInterpolated($@"
            SELECT d_asenta
            FROM dbo.tb_CodigosPostales
            WHERE d_codigo = {cp}
              AND IsDeleted = 0
        ")
            .AsNoTracking()
            .Select(x => x.d_asenta);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Contains(q));

        return await query
            .Distinct()
            .OrderBy(x => x)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>
    /// Normaliza CP (5 dígitos).
    /// </summary>
    private string? Normalize(string? cp)
    {
        if (string.IsNullOrWhiteSpace(cp)) return null;

        var digits = new string(cp.Where(char.IsDigit).ToArray());
        if (digits.Length == 4) digits = "0" + digits;
        if (digits.Length != 5) return null;

        return digits;
    }
}