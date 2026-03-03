namespace velios.Api.Models.Common;

/// <summary>
/// Modelo de configuración para el servicio SMTP.
///
/// Esta clase se utiliza para mapear la sección "Smtp"
/// del archivo appsettings.json mediante el patrón Options.
///
/// Permite centralizar la configuración de envío de correos
/// electrónicos (host, puerto, credenciales y remitente).
///
/// Ejemplo en appsettings.json:
/// 
/// "Smtp": {
///   "Host": "smtp.brevo.com",
///   "Port": 587,
///   "User": "usuario_smtp",
///   "Pass": "password_smtp",
///   "FromEmail": "no-reply@velios.com",
///   "FromName": "Velios"
/// }
/// </summary>
public class SmtpSettings
{
    /// <summary>
    /// Dirección del servidor SMTP.
    /// 
    /// Ejemplo: smtp.brevo.com
    /// </summary>
    public string Host { get; set; } = "";

    /// <summary>
    /// Puerto del servidor SMTP.
    /// 
    /// Valores comunes:
    /// 25  = Sin cifrado (no recomendado)
    /// 465 = SSL
    /// 587 = STARTTLS (recomendado)
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Usuario o login para autenticación SMTP.
    /// </summary>
    public string User { get; set; } = "";

    /// <summary>
    /// Contraseña o token de autenticación SMTP.
    /// 
    /// ⚠ Recomendación:
    /// No almacenar credenciales en texto plano en producción.
    /// Usar variables de entorno o Azure Key Vault.
    /// </summary>
    public string Pass { get; set; } = "";

    /// <summary>
    /// Dirección de correo desde la cual se enviarán los emails.
    /// </summary>
    public string FromEmail { get; set; } = "";

    /// <summary>
    /// Nombre visible del remitente.
    /// 
    /// Valor por defecto: "Velios".
    /// </summary>
    public string FromName { get; set; } = "Velios";
}