namespace velios.Api.Services.Email;

<<<<<<< HEAD
/// <summary>
/// Define el contrato para el servicio de envío de correos electrónicos.
/// 
/// Esta interfaz abstrae la implementación del proveedor de correo
/// (SMTP, API externa como SendGrid, Brevo, etc.), permitiendo
/// desacoplar la lógica de negocio del mecanismo de envío.
/// 
/// Implementa el principio de Inversión de Dependencias (DIP)
/// y facilita pruebas unitarias mediante mocking.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Envía un correo electrónico en formato HTML.
    /// </summary>
    /// <param name="to">
    /// Dirección de correo electrónico del destinatario.
    /// </param>
    /// <param name="subject">
    /// Asunto del mensaje.
    /// </param>
    /// <param name="htmlBody">
    /// Contenido del mensaje en formato HTML.
    /// </param>
    /// <returns>
    /// Una tarea asincrónica que representa la operación de envío.
    /// </returns>
    /// <remarks>
    /// - La implementación concreta puede usar SMTP o API HTTP.
    /// - Se recomienda que las implementaciones manejen excepciones
    ///   específicas como <see cref="System.Net.Mail.SmtpException"/>.
    /// - El método debe ser no bloqueante (async/await).
    /// </remarks>
    Task Send(string to, string subject, string htmlBody);
=======
public interface IEmailSender
{
    void Send(string to, string subject, string htmlBody);
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
}