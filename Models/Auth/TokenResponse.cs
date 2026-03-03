namespace velios.Api.Models.Auth;

public class TokenResponse
{
    public string bearerToken { get; set; } = "";
    public DateTime validFrom { get; set; }
    public DateTime validTo { get; set; }
}