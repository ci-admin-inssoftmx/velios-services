namespace velios.Api.Services.Email;

public interface IEmailSender
{
    void Send(string to, string subject, string htmlBody);
}