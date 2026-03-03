using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Auth;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Contraseña actual es requerida")]
    public string OldPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nueva contraseña es requerida")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$",
        ErrorMessage = "La contraseña debe tener al menos 8 caracteres, 1 mayúscula, 1 minúscula, 1 número y 1 carácter especial."
    )]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirmar contraseña es requerido")]
    [Compare("NewPassword", ErrorMessage = "La nueva contraseña y la confirmación no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}