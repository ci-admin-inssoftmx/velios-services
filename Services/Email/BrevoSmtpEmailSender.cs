using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using velios.Api.Models.Common;

namespace velios.Api.Services.Email;

<<<<<<< HEAD
/// <summary>
/// Implementación del servicio de envío de correos electrónicos
/// utilizando SMTP (Brevo / Sendinblue).
/// 
/// Esta clase implementa la interfaz <see cref="IEmailSender"/>
/// y permite enviar correos en formato HTML mediante conexión
/// segura (STARTTLS).
/// 
/// Requiere configuración previa en appsettings.json:
/// - Host
/// - Port
/// - User
/// - Pass
/// - FromEmail
/// - FromName
/// </summary>
=======
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
public class BrevoSmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;

<<<<<<< HEAD
    /// <summary>
    /// Constructor que inyecta la configuración SMTP desde el sistema
    /// de configuración de ASP.NET Core (IOptions pattern).
    /// </summary>
    /// <param name="options">
    /// Configuración SMTP proveniente de appsettings.json.
    /// </param>
=======
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
    public BrevoSmtpEmailSender(IOptions<SmtpSettings> options)
    {
        _settings = options.Value;
    }

<<<<<<< HEAD
    /// <summary>
    /// Envía un correo electrónico en formato HTML.
    /// </summary>
    /// <param name="to">Dirección de correo del destinatario.</param>
    /// <param name="subject">Asunto del mensaje.</param>
    /// <param name="htmlBody">Contenido HTML del correo.</param>
    /// <exception cref="SmtpException">
    /// Se lanza si ocurre un error de autenticación o conexión SMTP.
    /// </exception>
    /// <remarks>
    /// - Utiliza STARTTLS (SSL habilitado) para puerto 587.
    /// - No utiliza credenciales por defecto del sistema.
    /// - El tiempo de espera está configurado en 100 segundos.
    /// 
    /// Consideraciones de seguridad:
    /// - Las credenciales deben almacenarse en variables seguras
    ///   (Azure KeyVault, variables de entorno, etc.).
    /// - No se recomienda registrar contraseñas SMTP en logs.
    /// </remarks>
    public async Task Send(string to, string subject, string htmlBody)
    {
        using var mail = new MailMessage();

=======
    public void Send(string to, string subject, string htmlBody)
    {
        using var mail = new MailMessage();
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
        mail.From = new MailAddress(_settings.FromEmail, _settings.FromName);
        mail.To.Add(to);
        mail.Subject = subject;
        mail.Body = htmlBody;
        mail.IsBodyHtml = true;

<<<<<<< HEAD
        using var smtp = new SmtpClient(_settings.Host, _settings.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_settings.User, _settings.Pass),
            EnableSsl = true,        // STARTTLS para puerto 587
            Timeout = 100000
        };
=======
        using var smtp = new SmtpClient(_settings.Host, _settings.Port);
        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
        smtp.UseDefaultCredentials = false;
        smtp.Credentials = new NetworkCredential(_settings.User, _settings.Pass);
        smtp.EnableSsl = true;        // STARTTLS para 587
        smtp.Timeout = 100000;
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59

        smtp.Send(mail);
    }
}