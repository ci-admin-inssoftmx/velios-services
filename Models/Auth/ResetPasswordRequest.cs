using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Auth;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "idEmpleado es requerido")]
    public int IdEmpleado { get; set; }

    [Required(ErrorMessage = "Token es requerido")]
    public string Token { get; set; } = "";
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$",
        ErrorMessage = "La contraseña debe tener al menos 8 caracteres, 1 mayúscula, 1 minúscula, 1 número y 1 carácter especial."
    )]
    public string NewPassword { get; set; } = "";
}