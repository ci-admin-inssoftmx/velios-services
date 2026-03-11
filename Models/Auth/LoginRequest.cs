using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "Email es requerido")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Password es requerido")]

    public string Password { get; set; } = "";
}