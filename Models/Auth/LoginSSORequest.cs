using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Auth;

/// <summary>
/// Request para login SSO (Google).
/// Solo requiere el correo — Google ya autenticó al usuario.
/// </summary>
public class LoginSSORequest
{
    [Required(ErrorMessage = "El correo es requerido.")]
    [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
    public string Email { get; set; } = "";
}
