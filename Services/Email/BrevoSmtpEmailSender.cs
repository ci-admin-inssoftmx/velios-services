using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using velios.Api.Models.Common;

namespace velios.Api.Services.Email;

public class BrevoSmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;

    public BrevoSmtpEmailSender(IOptions<SmtpSettings> options)
    {
        _settings = options.Value;
    }

    public void Send(string to, string subject, string htmlBody)
    {
        using var mail = new MailMessage();
        mail.From = new MailAddress(_settings.FromEmail, _settings.FromName);
        mail.To.Add(to);
        mail.Subject = subject;
        mail.Body = htmlBody;
        mail.IsBodyHtml = true;

        using var smtp = new SmtpClient(_settings.Host, _settings.Port);
        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
        smtp.UseDefaultCredentials = false;
        smtp.Credentials = new NetworkCredential(_settings.User, _settings.Pass);
        smtp.EnableSsl = true;        // STARTTLS para 587
        smtp.Timeout = 100000;

        smtp.Send(mail);
    }
}