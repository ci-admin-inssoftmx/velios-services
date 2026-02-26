using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using velios.Api.Data;
using velios.Api.Models.Auth;
using velios.Api.Models.Common;
using velios.Api.Models.Security;
using velios.Api.Services.Email;

namespace velios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthenticateController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailSender _email;

    public AuthenticateController(AppDbContext db, IConfiguration config, IEmailSender email)
    {
        _db = db;
        _config = config;
        _email = email;
    }

    [HttpPost("SendActivationEmail")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> SendActivationEmail([FromBody] SendActivationRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Solicitud inválida.",
                statusCode = 400,
                data = null
            });
        }

        var email = (model.Email ?? "").Trim().ToLowerInvariant();

        // ✅ Guardar (si no existe) en dbo.tb_Proveedores(CorreoContacto)
        // CorreoContacto es UNIQUE + NOT NULL, así que lo hacemos idempotente.
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
            IF NOT EXISTS (SELECT 1 FROM dbo.tb_Proveedores WHERE CorreoContacto = {email})
            BEGIN
                INSERT INTO dbo.tb_Proveedores (CorreoContacto)
                VALUES ({email});
            END
        ");
        }
        catch
        {
            // No revelamos detalles (respuesta neutra). Puedes loguear internamente si deseas.
        }

        var token = CreateAccountActivationToken(email, TimeSpan.FromHours(24));

        var baseUrl = _config["Front:ActivationUrlBase"]
                      ?? "http://localhost:7137/WfActivarCuenta.aspx";

        var link = $"{baseUrl}?token={Uri.EscapeDataString(token)}&email={model.Email}";

        var subject = "Activa tu cuenta - Velios";
        var body = $@"
        <!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>¡Todo listo!</title>
</head>
<body style='margin: 0; padding: 0; background-color: #f5f5f5; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif;'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
        <tr>
            <td style='padding: 40px 20px;'>
                <table role='presentation' width='100%' max-width='600px' cellspacing='0' cellpadding='0' border='0' style='margin: 0 auto; background-color: #ffffff; border-radius: 12px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                   
                    <!-- Logo Header -->
                    <tr>
                        <td style='text-align: center; padding: 40px 20px;'>
                            <svg width='80' height='80' viewBox='0 0 80 80' style='margin-bottom: 10px;'>
                                <circle cx='40' cy='40' r='40' fill='#EC4E20'/>
                                <text x='40' y='55' font-family='Arial, sans-serif' font-size='40' font-weight='bold' fill='white' text-anchor='middle'>V</text>
                            </svg>
                            <h1 style='margin: 10px 0 5px 0; font-size: 36px; font-weight: 600; color: #2C3E50; letter-spacing: 2px;'>VELIOS</h1>
                            <p style='margin: 0; font-size: 14px; color: #999;'>Vigila tu operación en la última milla</p>
                        </td>
                    </tr>

                    <!-- Main Content -->
                    <tr>
                        <td style='padding: 0 60px 50px 60px;'>
                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                <tr>
                                    <td style='text-align: center;'>
                                        <h2 style='margin: 0 0 25px 0; font-size: 32px; font-weight: 600; color: #2C3E50;'>¡Todo listo!</h2>
                                       
                                        <p style='margin: 0 0 35px 0; font-size: 15px; color: #666; line-height: 1.6;'>
                                            Aquí está tu enlace de acceso único. Haz clic para continuar y empezar a usar la <strong>plataforma VELIOS</strong>
                                        </p>

                                        <!-- CTA Button -->
                                        <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                            <tr>
                                                <td style='text-align: center; padding: 20px 0;'>
                                                    <a href='{link}' style='display: inline-block; background-color: #EC4E20; color: #ffffff; text-decoration: none; padding: 16px 60px; border-radius: 8px; font-size: 16px; font-weight: 600; transition: background-color 0.3s;'>
                                                        Acceder ahora
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f9f9f9; padding: 30px; text-align: center; border-bottom-left-radius: 12px; border-bottom-right-radius: 12px;'>
                            <p style='margin: 0 0 10px 0; font-size: 13px; color: #666; line-height: 1.6;'>
                                Este correo fue enviado por la plataforma <strong>VELIOS</strong>
                            </p>
                            <p style='margin: 0 0 10px 0; font-size: 13px; color: #666; line-height: 1.6;'>
                                Plataforma para centralizar la gestión de riesgos, acciones y evidencia operativa en tiempo real como empresa y proveedor.
                            </p>
                            <p style='margin: 20px 0 0 0; font-size: 12px; color: #999;'>
                                Si no lo reconoces,
                                <a href='mailto:soporte@velios.com' style='color: #EC4E20; text-decoration: underline;'>haz clic aquí para cancelar la acción</a>.
                            </p>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        // Siempre manda, aunque el correo no exista en tu BD
        _email.Send(email, subject, body);

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Si el correo es válido, se enviará un enlace de activación.",
            statusCode = 200,
            data = null
        });
    }

    // =========================================================
    // POST /api/Authenticate/ActivateAccount
    // Guarda en dbo.tb_Proveedores(CorreoContacto) y valida token:
    // - Si token tiene 3 partes => valida JWT (issuer/audience/key/lifetime)
    // - Si token tiene 2 partes => valida token HMAC (CreateAccountActivationToken)
    // =========================================================
    [HttpPost("ActivateAccount")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ActivateAccount([FromBody] ActivateAccountRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Solicitud inválida.",
                statusCode = 400,
                data = null
            });
        }

        var email = (model.Email ?? "").Trim().ToLowerInvariant();
        var token = (model.Token ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Token y email son obligatorios.",
                statusCode = 400,
                data = null
            });
        }

        // 1) Validar token (JWT o HMAC activation)
        var secret = _config["Jwt:Key"]!;
        var parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries);

        string tokenEmail = "";
        bool tokenOk;

        if (parts.Length == 3)
        {
            tokenOk = TryValidateJwtAndGetEmail(token, out tokenEmail);

            if (!tokenOk)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "JWT inválido o expirado.",
                    statusCode = 401,
                    data = null
                });
            }

            tokenEmail = (tokenEmail ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(tokenEmail))
            {
                return Unauthorized(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "El JWT no contiene el claim de email.",
                    statusCode = 401,
                    data = null
                });
            }

            if (!string.Equals(tokenEmail, email, StringComparison.Ordinal))
            {
                return Unauthorized(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "El email no coincide con el token.",
                    statusCode = 401,
                    data = null
                });
            }
        }
        else
        {
            // Token de activación HMAC: base64url(payload).sig, payload=email|expTicks
            tokenOk = TryValidateAccountActivationToken(token, secret, out tokenEmail, out _);

            if (!tokenOk)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Token de activación inválido o expirado.",
                    statusCode = 401,
                    data = null
                });
            }

            if (!string.Equals(tokenEmail, email, StringComparison.Ordinal))
            {
                return Unauthorized(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "El email no coincide con el token.",
                    statusCode = 401,
                    data = null
                });
            }
        }

        // 2) ✅ ACTIVAR (UPDATE) el proveedor que ya se creó en SendActivationEmail
        //    Evita el error del índice UNIQUE de RFC con NULL (porque ya no INSERTAS otra fila)
        try
        {
            // (Opcional pero recomendado) No re-activar si está borrado
            // (Opcional) Asegura que exista primero
            var rows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE dbo.tb_Proveedores
            SET EstatusProveedorId = 1,           -- ACTIVO
                DateModified = {DateTime.UtcNow},
                ModifiedBy = 'ACTIVATION',
                IsDeleted = COALESCE(IsDeleted, 0)
            WHERE CorreoContacto = {email}
              AND (IsDeleted IS NULL OR IsDeleted = 0);
        ");

            if (rows == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "No existe un proveedor pendiente para ese correo. Solicita primero el enlace de activación.",
                    statusCode = 400,
                    data = null
                });
            }

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Cuenta activada con éxito.",
                statusCode = 200,
                data = new { email }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al activar la cuenta.",
                statusCode = 400,
                data = null,
                errors = new List<string> { ex.Message }
            });
        }
    }

    // =========================
    // Helpers: Hash (legacy)
    // =========================
    private static string HashPasswordLegacy(string password)
    {
        const string salt = "AllD0H345@LTHY!!";
        var passwordSalt = (password ?? "") + salt;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(passwordSalt);
        var hash = sha256.ComputeHash(bytes);

        return Convert.ToBase64String(hash);
    }

    // =========================
    // Helpers: Password reset token (NO extra table)
    // Token = base64url(payload).base64url(hmac(payload))
    // payload = idEmpleado|expUtcTicks|passwordEncriptadoSnapshot
    // =========================
    private string CreatePasswordResetToken(AccesoUsuarioColaborador user, TimeSpan ttl)
    {
        var expTicks = DateTime.UtcNow.Add(ttl).Ticks;

        // Snapshot actual del hash para invalidar token si el password cambia
        var snapshot = (user.PasswordEncriptado ?? "").Trim();

        // payload = idEmpleado|expTicks|snapshot
        var payload = $"{user.IdEmpleado}|{expTicks}|{snapshot}";

        var secret = _config["Jwt:Key"]!;
        var sig = ComputeHmacBase64Url(payload, secret);

        return $"{Base64UrlEncode(payload)}.{sig}";
    }

    private bool TryValidatePasswordResetToken(
        string token,
        string secret,
        out int idEmpleado,
        out long expTicks,
        out string passwordEncriptadoSnapshot)
    {
        idEmpleado = 0;
        expTicks = 0;
        passwordEncriptadoSnapshot = "";

        var parts = (token ?? "").Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;

        string payload;
        try { payload = Base64UrlDecode(parts[0]); }
        catch { return false; }

        var sig = parts[1];

        var expectedSig = ComputeHmacBase64Url(payload, secret);
        if (!FixedTimeEquals(sig, expectedSig)) return false;

        var fields = payload.Split('|');
        if (fields.Length != 3) return false;

        // 1) IdEmpleado
        if (!int.TryParse(fields[0], out idEmpleado) || idEmpleado <= 0) return false;

        // 2) Expiración
        if (!long.TryParse(fields[1], out expTicks)) return false;
        if (DateTime.UtcNow.Ticks > expTicks) return false;

        // 3) Snapshot
        passwordEncriptadoSnapshot = (fields[2] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(passwordEncriptadoSnapshot)) return false;

        return true;
    }

    private static string ComputeHmacBase64Url(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Base64UrlEncode(hash);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a ?? "");
        var bb = Encoding.UTF8.GetBytes(b ?? "");
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static string Base64UrlEncode(string text) =>
        Base64UrlEncode(Encoding.UTF8.GetBytes(text));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static string Base64UrlDecode(string base64Url)
    {
        var s = (base64Url ?? "").Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        var bytes = Convert.FromBase64String(s);
        return Encoding.UTF8.GetString(bytes);
    }

    // =========================
    // Helpers: JWT
    // =========================
    private TokenResponse CreateJwt(AccesoUsuarioColaborador user)
    {
        var key = _config["Jwt:Key"]!;
        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.IdEmpleado.ToString()),
            new(ClaimTypes.Name, user.UsuarioColaborador),
            new("TipoAcceso", user.TipoAcceso.ToString())
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var validFrom = DateTime.UtcNow;
        var validTo = validFrom.AddHours(24); // ajusta la vida del token

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: validFrom,
            expires: validTo,
            signingCredentials: creds);

        return new TokenResponse
        {
            bearerToken = new JwtSecurityTokenHandler().WriteToken(token),
            validFrom = validFrom,
            validTo = validTo
        };
    }

    // Valida un JWT con la misma configuración (issuer/audience/key/lifetime)
    // y trata de extraer email desde claims: ClaimTypes.Email, "email" o "upn".
    private bool TryValidateJwtAndGetEmail(string jwt, out string email)
    {
        email = "";

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                ValidIssuer = _config["Jwt:Issuer"],
                ValidAudience = _config["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            var principal = tokenHandler.ValidateToken(jwt, parameters, out _);

            email =
                principal.FindFirstValue(ClaimTypes.Email) ??
                principal.FindFirstValue("email") ??
                principal.FindFirstValue("upn") ??
                "";

            return true;
        }
        catch
        {
            return false;
        }
    }

    // =========================
    // POST /api/Authenticate/Login
    // =========================
    [HttpPost("Login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginDataResponse>>> Login([FromBody] LoginRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<LoginDataResponse>
                {
                    success = false,
                    request_id = requestId,
                    message = "Solicitud inválida: verifique los datos ingresados.",
                    data = null
                });
            }

            var userName = (model.UsuarioColaborador ?? "").Trim();
            var password = model.Password ?? "";

            var user = await _db.AccesosUsuarios
                .FirstOrDefaultAsync(x => x.UsuarioColaborador == userName);

            if (user == null || user.IdEstatus != 1)
            {
                return Unauthorized(new ApiResponse<LoginDataResponse>
                {
                    request_id = requestId,
                    success = false,
                    message = "Credenciales inválidas.",
                    data = null,
                    statusCode = 401,
                    errors = new List<string> { "Usuario o contraseña incorrectos." }
                });
            }

            if (user.Intentos >= 5)
            {
                return Unauthorized(new ApiResponse<LoginDataResponse>
                {
                    request_id = requestId,
                    success = false,
                    message = "Cuenta bloqueada por intentos fallidos.",
                    data = null,
                    statusCode = 401,
                    errors = new List<string> { "Cuenta bloqueada. Contacte a soporte." }
                });
            }

            var inputHash = HashPasswordLegacy(password);
            var storedHash = (user.PasswordEncriptado ?? "").Trim();

            if (!string.Equals(inputHash, storedHash, StringComparison.Ordinal))
            {
                user.Intentos += 1;
                await _db.SaveChangesAsync();

                return Unauthorized(new ApiResponse<LoginDataResponse>
                {
                    request_id = requestId,
                    success = false,
                    message = "Credenciales inválidas.",
                    data = null,
                    statusCode = 401,
                    errors = new List<string> { "Usuario o contraseña incorrectos." }
                });
            }

            // Login OK
            var empleado = await _db.Empleados
                .FromSqlInterpolated($@"
        SELECT
            IdEmpleado,
            NoColaborador,
            dbo.decryptValue(Nombres) AS Nombres,
            dbo.decryptValue(ApellidoPaterno) AS ApellidoPaterno,
            dbo.decryptValue(ApellidoMaterno) AS ApellidoMaterno,
            dbo.decryptValue(Calle) AS Calle,
            dbo.decryptValue(Numero) AS Numero,
            IdCP,
            dbo.decryptValue(EMail) AS EMail
        FROM dbo.tb_Empleados
        WHERE IdEmpleado = {user.IdEmpleado}
    ")
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (empleado == null)
            {
                return Unauthorized(new ApiResponse<LoginDataResponse>
                {
                    request_id = requestId,
                    success = false,
                    message = "Empleado no encontrado.",
                    statusCode = 401
                });
            }

            user.Intentos = 0;
            user.FechaUltimoAcceso = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var token = CreateJwt(user);

            var data = new LoginDataResponse
            {
                Email = empleado.EMail ?? "",
                IdUsuario = user.IdAccesoUsuarioColaborador.ToString(),
                EmpleadoId = empleado.IdEmpleado,
                NumeroEmpleado = empleado.NoColaborador ?? "",

                Nombres = empleado.Nombres,
                ApellidoPaterno = empleado.ApellidoPaterno ?? "",
                ApellidoMaterno = empleado.ApellidoMaterno ?? "",

                IdUnidad = 0,
                NombreUnidad = "",
                NombreTipoUnidad = "",

                HorarioId = 0,
                Horarios = new List<HorarioDto>(),

                UnidadDireccion = new UnidadDireccionDto
                {
                    Calle = empleado.Calle ?? "",
                    NumeroInterior = "",
                    NumeroExterior = empleado.Numero ?? "",
                    EstadoId = 0,
                    Estado = "",
                    ColoniaId = 0,
                    Colonia = "",
                    MunicipioId = 0,
                    Municipio = "",
                    CodigoPostalId = empleado.IdCP ?? 0,
                    CodigoPostal = ""
                },

                Roles = new List<string> { "Admin" },
                Token = token
            };

            return Ok(new ApiResponse<LoginDataResponse>
            {
                request_id = requestId,
                success = true,
                message = "Solicitud ejecutada con éxito.",
                data = data,
                statusCode = 200,
                field = null,
                code = null,
                errors = null
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                message = "Solicitud inválida: verifique los datos ingresados.",
                detail = ex.Message
            });
        }
    }

    // =========================
    // POST /api/Authenticate/ForgotPassword
    // (sin tabla extra: token firmado con exp)
    // =========================
    [HttpPost("ForgotPassword")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword([FromBody] ForgotPasswordRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        // (Opcional) valida modelo si tu ForgotPasswordRequest tiene Required
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Solicitud inválida: verifique los datos ingresados.",
                data = null,
                statusCode = 400
            });
        }

        var userName = (model.Email ?? "").Trim();

        var user = await _db.AccesosUsuarios
            .FirstOrDefaultAsync(x => x.UsuarioColaborador == userName);

        // Respuesta neutra para no filtrar existencia
        if (user == null)
        {
            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Si el usuario existe se enviará un enlace.",
                data = null,
                statusCode = 200
            });
        }

        // (IMPORTANTE) Obtener el correo real del empleado
        // En tu Login ya haces un SELECT a tb_Empleados para sacar EMail.
        // Reusamos la misma idea aquí para mandar al correo real.
        var empleado = await _db.Empleados
            .FromSqlInterpolated($@"
            SELECT
                IdEmpleado,
                dbo.decryptValue(EMail) AS EMail
            FROM dbo.tb_Empleados
            WHERE IdEmpleado = {user.IdEmpleado}
        ")
            .AsNoTracking()
            .FirstOrDefaultAsync();

        var correoDestino = (empleado?.EMail ?? "").Trim();
        if (string.IsNullOrWhiteSpace(correoDestino))
        {
            // Sigue siendo neutro, para no filtrar info
            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Si el usuario existe se enviará un enlace.",
                data = null,
                statusCode = 200
            });
        }

        // Genera token 15 min
        var token = CreatePasswordResetToken(user, TimeSpan.FromMinutes(15));

        // Link al FRONT (la pantalla que capturará uid + token)
        var baseUrl = _config["Front:ResetPasswordUrlBase"] ?? "";
        // ejemplo: https://.../reset-password?uid=15&token=...
        var resetLink = $"{baseUrl}?uid={user.IdEmpleado}&token={Uri.EscapeDataString(token)}";

        // HTML básico
        var asunto = "Restablecer contraseña";
        var body = $@"
        <h2>Restablecer contraseña</h2>
        <p>Se solicitó un restablecimiento de contraseña.</p>
        <p><a href=""{resetLink}"">Haz clic aquí para restablecer tu contraseña</a></p>
        <p>Este enlace expira en 15 minutos.</p>
        <p>Si tú no solicitaste esto, puedes ignorar este mensaje.</p>
    ";

        try
        {
            SendEmailBrevoSmtp(correoDestino, asunto, body);
        }
        catch
        {
            // Si falla el SMTP, NO conviene revelar mucho; puedes loguear internamente.
            // Regresa neutro para evitar enumeración de usuarios.
            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Si el usuario existe se enviará un enlace.",
                data = null,
                statusCode = 200
            });
        }

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Si el usuario existe se enviará un enlace.",
            data = null,
            statusCode = 200
        });
    }

    // =========================
    // POST /api/Authenticate/ResetPassword
    // =========================
    [HttpPost("ResetPassword")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword([FromBody] ResetPasswordRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Solicitud inválida: verifique los datos ingresados.",
                data = null,
                statusCode = 400
            });
        }

        var secret = _config["Jwt:Key"]!;

        if (!TryValidatePasswordResetToken(model.Token, secret, out int idEmpleadoToken, out _, out var snapshot))
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Token inválido o expirado.",
                data = null,
                statusCode = 400
            });
        }

        if (idEmpleadoToken != model.IdEmpleado)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Token no corresponde al usuario.",
                data = null,
                statusCode = 400
            });
        }

        var user = await _db.AccesosUsuarios
            .FirstOrDefaultAsync(x => x.IdEmpleado == model.IdEmpleado);

        if (user == null)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Usuario inválido.",
                data = null,
                statusCode = 400
            });
        }

        // Snapshot validation
        if (!string.Equals((user.PasswordEncriptado ?? "").Trim(), snapshot, StringComparison.Ordinal))
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Token ya no es válido.",
                data = null,
                statusCode = 400
            });
        }

        // Nueva != actual
        var newHash = HashPasswordLegacy(model.NewPassword);
        if (string.Equals(user.PasswordEncriptado, newHash, StringComparison.Ordinal))
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "La nueva contraseña no puede ser la misma que la actual.",
                data = null,
                statusCode = 400
            });
        }

        // Guardar
        user.PasswordEncriptado = newHash;
        user.ContraseniaColaborador = model.NewPassword;
        user.EstatusCambioContrasena = 0;
        user.Intentos = 0;

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Contraseña restablecida con éxito.",
            data = null,
            statusCode = 200
        });
    }

    // =========================
    // POST /api/Authenticate/ChangePassword
    // =========================
    [HttpPost("ChangePassword")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] ChangePasswordRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    request_id = requestId,
                    message = "Solicitud inválida: verifique los datos ingresados.",
                    data = null
                });
            }

            var empleadoIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(empleadoIdStr))
            {
                return Unauthorized(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "No autorizado.",
                    data = null,
                    statusCode = 401
                });
            }

            var empleadoId = int.Parse(empleadoIdStr);

            var user = await _db.AccesosUsuarios
                .FirstOrDefaultAsync(x => x.IdEmpleado == empleadoId);

            if (user == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "No autorizado.",
                    data = null,
                    statusCode = 401
                });
            }

            var currentHash = HashPasswordLegacy(model.OldPassword ?? "");

            if (!string.Equals(
                    (user.PasswordEncriptado ?? "").Trim(),
                    currentHash,
                    StringComparison.Ordinal))
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Contraseña actual incorrecta.",
                    data = null,
                    statusCode = 400,
                    errors = new List<string> { "Contraseña actual incorrecta." }
                });
            }

            if (string.Equals(
                    model.OldPassword?.Trim(),
                    model.NewPassword?.Trim(),
                    StringComparison.Ordinal))
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "La nueva contraseña no puede ser igual a la actual.",
                    statusCode = 400,
                    errors = new List<string>
                    {
                        "La nueva contraseña no puede ser igual a la actual."
                    }
                });
            }

            user.PasswordEncriptado = HashPasswordLegacy(model.NewPassword ?? "");
            user.ContraseniaColaborador = model.NewPassword;
            user.EstatusCambioContrasena = 0;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Solicitud ejecutada con éxito.",
                data = null,
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                message = "Solicitud inválida: verifique los datos ingresados.",
                detail = ex.Message
            });
        }
    }

    private void SendEmailBrevoSmtp(string toEmail, string subject, string htmlBody)
    {
        var host = _config["Smtp:Host"]!;
        var port = int.Parse(_config["Smtp:Port"] ?? "587");
        var user = _config["Smtp:User"]!;
        var pass = _config["Smtp:Pass"]!;
        var fromEmail = _config["Smtp:FromEmail"]!;
        var fromName = _config["Smtp:FromName"] ?? "";

        using var mail = new MailMessage();
        mail.From = string.IsNullOrWhiteSpace(fromName)
            ? new MailAddress(fromEmail)
            : new MailAddress(fromEmail, fromName);

        mail.To.Add(toEmail);
        mail.Subject = subject;
        mail.Body = htmlBody;
        mail.IsBodyHtml = true;

        using var smtp = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = true
        };

        smtp.Send(mail);
    }

    // =========================
    // Helpers: Account activation token (NO DB lookup)
    // Token = base64url(payload).base64url(hmac(payload))
    // payload = email|expUtcTicks
    // =========================
    private string CreateAccountActivationToken(string email, TimeSpan ttl)
    {
        var expTicks = DateTime.UtcNow.Add(ttl).Ticks;
        var payload = $"{email.Trim().ToLowerInvariant()}|{expTicks}";

        var secret = _config["Jwt:Key"]!;
        var sig = ComputeHmacBase64Url(payload, secret);

        return $"{Base64UrlEncode(payload)}.{sig}";
    }

    private bool TryValidateAccountActivationToken(
        string token,
        string secret,
        out string email,
        out long expTicks)
    {
        email = "";
        expTicks = 0;

        var parts = (token ?? "").Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;

        string payload;
        try { payload = Base64UrlDecode(parts[0]); }
        catch { return false; }

        var sig = parts[1];
        var expectedSig = ComputeHmacBase64Url(payload, secret);
        if (!FixedTimeEquals(sig, expectedSig)) return false;

        var fields = payload.Split('|');
        if (fields.Length != 2) return false;

        email = (fields[0] ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) return false;

        if (!long.TryParse(fields[1], out expTicks)) return false;
        if (DateTime.UtcNow.Ticks > expTicks) return false;

        return true;
    }

}