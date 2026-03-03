using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "Usuario es requerido")]
    public string UsuarioColaborador { get; set; } = "";
    [Required(ErrorMessage = "Password es requerido")]

    public string Password { get; set; } = "";
}