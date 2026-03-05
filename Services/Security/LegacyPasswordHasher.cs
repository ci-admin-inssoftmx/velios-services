using System.Security.Cryptography;
using System.Text;

namespace velios.Api.Services.Security;

/// <summary>
/// Implementa el hash de contraseñas "legacy" usando SHA-256 con un salt fijo.
/// </summary>
/// <remarks>
/// ⚠️ Seguridad:
/// - SHA256 + salt fijo NO es lo ideal para contraseñas en producción.
/// - Se mantiene por compatibilidad con contraseñas existentes.
/// - Recomendación futura: migrar a bcrypt/argon2.
/// </remarks>
public class LegacyPasswordHasher : IPasswordHasher
{
    private const string Salt = "AllD0H345@LTHY!!";

    /// <inheritdoc />
    public string HashLegacy(string password)
    {
        var passwordSalt = (password ?? "") + Salt;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(passwordSalt);
        var hash = sha256.ComputeHash(bytes);

        return Convert.ToBase64String(hash);
    }
}