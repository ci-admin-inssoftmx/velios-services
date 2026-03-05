namespace velios.Api.Models.Security;

public class ResetToken
{
    public int Id { get; set; }
    public string UsuarioColaborador { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTime Expira { get; set; }
    public bool Usado { get; set; }
}