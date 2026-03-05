using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Auth.Security
{
    public class SetProveedorPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        // Opcional: si viene vacío, se genera una
        public string? Password { get; set; }

        [Required]
        public string Token { get; set; } = "";
    }
}