namespace velios.Api.Services.Security;

/// <summary>
/// Define el contrato para el servicio de hash de contraseñas.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Calcula un hash (legacy) de la contraseña en texto plano.
    /// </summary>
    /// <param name="password">Contraseña en texto plano.</param>
    /// <returns>Hash en Base64.</returns>
    string HashLegacy(string password);
}