namespace velios.Api.Models.Auth;

public class AuthResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}