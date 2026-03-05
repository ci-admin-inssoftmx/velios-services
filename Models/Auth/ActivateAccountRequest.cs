namespace velios.Api.Models.Auth
{
    public class ActivateAccountRequest
    {
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
