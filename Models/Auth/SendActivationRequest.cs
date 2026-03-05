using System.ComponentModel.DataAnnotations;

namespace velios.Api.Models.Auth;

public class SendActivationRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}