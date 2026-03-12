using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using velios.Api.Data;
using velios.Api.Models.Auth;
using velios.Api.Models.Auth.Security;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;
using velios.Api.Models.Security;
using velios.Api.Services.Email;
using velios.Api.Services.Security;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador de autenticación y seguridad de Velios.
///
/// Funcionalidades principales:
/// - Activación de cuenta de proveedor (correo + token).
/// - Login de colaboradores (JWT).
/// - Recuperación/restablecimiento/cambio de contraseña de colaboradores.
/// - Establecimiento de contraseña para proveedor usando token de activación.
///
/// Seguridad:
/// - Respuestas neutras en recuperación de contraseña (evita enumeración).
/// - Tokens HMAC sin tabla adicional (expiran por ticks UTC).
/// - Hash de password "legacy" (SHA-256 + salt fijo) por compatibilidad.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthenticateController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailSender _email;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthenticateController> _logger;

    /// <summary>
    /// Constructor con inyección de dependencias.
    /// </summary>
    /// <param name="db">DbContext EF Core.</param>
    /// <param name="config">Configuración (Jwt, Front URLs, Smtp).</param>
    /// <param name="email">Servicio de envío de correo (abstracción).</param>
    public AuthenticateController(AppDbContext db, IConfiguration config, IEmailSender email, IPasswordHasher passwordHasher, ILogger<AuthenticateController> logger)
    {
        _db = db;
        _config = config;
        _email = email;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    // =========================================================
    // POST /api/Authenticate/SendActivationEmail
    // =========================================================

    /// <summary>
    /// Envía un correo con enlace de activación de cuenta para proveedor.
    ///
    /// Flujo:
    /// 1) Normaliza email.
    /// 2) Inserta proveedor si no existe (idempotente por CorreoContacto UNIQUE).
    /// 3) Genera token HMAC con expiración.
    /// 4) Construye link al front y envía correo.
    /// </summary>
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

        // Guardar proveedor si no existe (idempotente).
        try
        {
            var exists = await _db.Proveedores
                .AnyAsync(x => x.CorreoContacto == email && !x.IsDeleted);

            if (!exists)
            {
                var proveedor = new Proveedor
                {
                    RFC = null,
                    CorreoContacto = email,
                    IsDeleted = false,
                    DateCreated = DateTime.UtcNow,
                    EstatusProveedorId = 4 
                };

                _db.Proveedores.Add(proveedor);
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error insertando proveedor para email {Email}", email);
        }

        var token = CreateAccountActivationToken(email, TimeSpan.FromHours(24));

        var baseUrl = _config["Front:ActivationUrlBase"]
                      ?? "http://localhost:7137/WfActivarCuenta.aspx";

        var link = $"{baseUrl}?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(model.Email ?? email)}";

        var subject = "Activa tu cuenta - Velios";
        var body = $@"
<!DOCTYPE html>
<html lang='es'>
<head>
  <meta charset='UTF-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
  <title>Activa tu cuenta</title>
</head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
  <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
    <tr>
      <td style='padding:40px 20px;'>
        <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
               style='margin:0 auto;max-width:600px;background:#fff;border-radius:12px;box-shadow:0 4px 6px rgba(0,0,0,0.1);'>
          <tr>
            <td style='text-align:center;padding:40px 20px;'>
              <h1 style='margin:0;color:#2C3E50;'>VELIOS</h1>
              <p style='margin:8px 0 0 0;color:#999;'>Vigila tu operación en la última milla</p>
            </td>
          </tr>

          <tr>
            <td style='padding:0 60px 40px 60px;text-align:center;'>
              <h2 style='margin:0 0 15px 0;color:#2C3E50;'>¡Todo listo!</h2>
              <p style='margin:0 0 25px 0;color:#666;line-height:1.6;'>
                Haz clic para activar tu cuenta y continuar en la plataforma.
              </p>

              <a href='{link}'
                 style='display:inline-block;background:#EC4E20;color:#fff;text-decoration:none;padding:14px 42px;border-radius:8px;font-weight:600;'>
                 Activar cuenta
              </a>

              <p style='margin:28px 0 0 0;color:#999;font-size:12px;'>
                Si no solicitaste esto, ignora este correo.
              </p>
            </td>
          </tr>

          <tr>
            <td style='background:#f9f9f9;padding:20px;text-align:center;border-bottom-left-radius:12px;border-bottom-right-radius:12px;'>
              <p style='margin:0;color:#999;font-size:12px;'>Este correo fue enviado por VELIOS.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

        await _email.Send(email, subject, body);

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
    // =========================================================

    /// <summary>
    /// Activa cuenta de proveedor validando token (JWT o HMAC).
    /// Actualiza EstatusProveedorId = 1 (ACTIVO) para el CorreoContacto.
    /// </summary>
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

        // Validación token (JWT o HMAC)
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
        }
        else
        {
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

        try
        {
            var proveedor = await _db.Proveedores
                .FirstOrDefaultAsync(p =>
                    p.CorreoContacto.ToLower() == email &&
                    (p.IsDeleted == null || p.IsDeleted == false));

            if (proveedor == null)
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

            // Activar proveedor
            proveedor.EstatusProveedorId = 1;
            proveedor.DateModified = DateTime.UtcNow;
            proveedor.ModifiedBy = "ACTIVATION";
            proveedor.IsDeleted = proveedor.IsDeleted;

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Cuenta activada con éxito.",
                statusCode = 200,
                data = new
                {
                    proveedorId = proveedor.ProveedorId,
                    email = proveedor.CorreoContacto
                }
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

    // =========================================================
    // POST /api/Authenticate/Login
    // =========================================================

    /// <summary>
    /// Autentica un colaborador por usuario + contraseña.
    /// - Valida estado y bloqueos por intentos.
    /// - Compara hash legacy.
    /// - Retorna JWT + datos del empleado.
    /// </summary>
    // =========================================================
    // POST /api/Authenticate/Login
    // =========================================================

    /// <summary>
    /// Autentica un proveedor por correo + contraseña.
    /// - Busca en tb_Proveedores.
    /// - Valida que el proveedor esté activo.
    /// - Compara PasswordHash con hash legacy.
    /// - Retorna JWT + datos básicos del proveedor.
    /// </summary>
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
                    request_id = requestId,
                    success = false,
                    message = "Solicitud inválida: verifique los datos ingresados.",
                    data = null,
                    statusCode = 400
                });
            }

            var email = (model.Email ?? "").Trim().ToLowerInvariant();
            var password = model.Password ?? "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(new ApiResponse<LoginDataResponse>
                {
                    request_id = requestId,
                    success = false,
                    message = "Email y contraseña son obligatorios.",
                    data = null,
                    statusCode = 400
                });
            }

            // Buscar proveedor por correo
            var proveedor = await _db.Proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.CorreoContacto != null &&
                    x.CorreoContacto.ToLower() == email &&
                    !x.IsDeleted);

            if (proveedor == null)
            {
                return Unauthorized(new ApiResponse<LoginDataResponse>
                {
                    request_id = requestId,
                    success = false,
                    message = "Credenciales inválidas.",
                    data = null,
                    statusCode = 401,
                    errors = new List<string> { "Correo o contraseña incorrectos." }
                });
            }

            // Validar que el proveedor esté activo
            if (proveedor.EstatusProveedorId != 1)
            {
                return Unauthorized(new ApiResponse<LoginDataResponse>
                {
                    request_id = requestId,
                    success = false,
                    message = "La cuenta del proveedor no está activa.",
                    data = null,
                    statusCode = 401,
                    errors = new List<string> { "La cuenta aún no está activa o fue deshabilitada." }
                });
            }

            // Comparar hash legacy
            var hash = _passwordHasher.HashLegacy(password);
            var storedHash = (proveedor.PasswordHash ?? "").Trim();

            if (!string.Equals(hash, storedHash, StringComparison.Ordinal))
            {
                return Unauthorized(new ApiResponse<LoginDataResponse>
                {
                    request_id = requestId,
                    success = false,
                    message = "Credenciales inválidas.",
                    data = null,
                    statusCode = 401,
                    errors = new List<string> { "Correo o contraseña incorrectos." }
                });
            }

            // Login OK: generar JWT de proveedor
            var token = CreateJwtProveedor(proveedor);

            var data = new LoginDataResponse
            {
                Email = proveedor.CorreoContacto ?? "",
                IdUsuario = proveedor.ProveedorId.ToString(),

                // No aplica para proveedor, pero se llenan por compatibilidad
                EmpleadoId = proveedor.ProveedorId,
                NumeroEmpleado = proveedor.ProveedorId.ToString(),

                // Reutilizamos estos campos para mostrar nombre del proveedor
                Nombres = proveedor.NombreComercial ?? proveedor.RazonSocial ?? "",
                ApellidoPaterno = "",
                ApellidoMaterno = "",

                IdUnidad = 0,
                NombreUnidad = "",
                NombreTipoUnidad = "",

                HorarioId = 0,
                Horarios = new List<HorarioDto>(),

                UnidadDireccion = new UnidadDireccionDto
                {
                    Calle = proveedor.Calle ?? "",
                    NumeroInterior = "",
                    NumeroExterior = "",
                    EstadoId = 0,
                    Estado = proveedor.Estado ?? "",
                    ColoniaId = 0,
                    Colonia = proveedor.Colonia ?? "",
                    MunicipioId = 0,
                    Municipio = proveedor.DelegacionMunicipio ?? "",
                    CodigoPostalId = 0,
                    CodigoPostal = proveedor.CodigoPostal ?? ""
                },

                Roles = new List<string> { "Proveedor" },
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
            _logger.LogError(ex, "Error en login de proveedor para email {Email}", model.Email);

            return BadRequest(new ApiResponse<LoginDataResponse>
            {
                request_id = requestId,
                success = false,
                message = "Error al iniciar sesión.",
                data = null,
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    private TokenResponse CreateJwtProveedor(Proveedor proveedor)
    {
        var key = _config["Jwt:Key"]!;
        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];

        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, proveedor.ProveedorId.ToString()),
        new Claim(ClaimTypes.Name, proveedor.CorreoContacto ?? ""),
        new Claim(ClaimTypes.Email, proveedor.CorreoContacto ?? ""),
        new Claim(ClaimTypes.Role, "Proveedor"),
        new Claim("ProveedorId", proveedor.ProveedorId.ToString()),
        new Claim("RazonSocial", proveedor.RazonSocial ?? ""),
        new Claim("NombreComercial", proveedor.NombreComercial ?? "")
    };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var validFrom = DateTime.UtcNow;
        var validTo = validFrom.AddHours(24);

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

    // =========================================================
    // POST /api/Authenticate/ForgotPassword
    // =========================================================

    /// <summary>
    /// Inicia recuperación de contraseña de colaborador.
    /// Respuesta neutra para no filtrar existencia.
    /// </summary>
    [HttpPost("ForgotPassword")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword([FromBody] ForgotPasswordRequest model)
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

        var email = (model.Email ?? "").Trim().ToLowerInvariant();

        // Buscar proveedor por correo
        var proveedor = await _db.Proveedores
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.CorreoContacto != null &&
                p.CorreoContacto.ToLower() == email &&
                !p.IsDeleted);

        // Respuesta neutra para evitar enumeración de usuarios
        if (proveedor == null)
        {
            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Si el correo existe se enviará un enlace para restablecer la contraseña.",
                data = null,
                statusCode = 200
            });
        }

        var correoDestino = proveedor.CorreoContacto;

        if (string.IsNullOrWhiteSpace(correoDestino))
        {
            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Si el correo existe se enviará un enlace para restablecer la contraseña.",
                data = null,
                statusCode = 200
            });
        }

        // Token de recuperación válido por 15 minutos
        var token = CreateAccountActivationToken(correoDestino, TimeSpan.FromMinutes(15));

        var baseUrl = _config["Front:ResetPasswordUrlBase"] ?? "";
        var resetLink = $"{baseUrl}?email={Uri.EscapeDataString(correoDestino)}&token={Uri.EscapeDataString(token)}";

        var subject = "Restablecer contraseña - Velios";

        var body = $@"
        <h2>Restablecer contraseña</h2>
        <p>Se solicitó un restablecimiento de contraseña para tu cuenta de proveedor.</p>
        <p>
            <a href=""{resetLink}"">
                Haz clic aquí para restablecer tu contraseña
            </a>
        </p>
        <p>Este enlace expira en 15 minutos.</p>
        <p>Si tú no solicitaste esto, puedes ignorar este mensaje.</p>
    ";

        try
        {
            await _email.Send(correoDestino, subject, body);
        }
        catch
        {
            // respuesta neutra
            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Si el correo existe se enviará un enlace para restablecer la contraseña.",
                data = null,
                statusCode = 200
            });
        }

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Si el correo existe se enviará un enlace para restablecer la contraseña.",
            data = null,
            statusCode = 200
        });
    }

    // =========================================================
    // POST /api/Authenticate/ResetPassword
    // =========================================================

    /// <summary>
    /// Restablece contraseña de colaborador usando token firmado (HMAC + expiración).
    /// </summary>
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

        var email = (model.Email ?? "").Trim().ToLowerInvariant();
        var token = (model.Token ?? "").Trim();
        var newPassword = (model.NewPassword ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(newPassword))
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Email, token y nueva contraseña son obligatorios.",
                data = null,
                statusCode = 400
            });
        }

        var secret = _config["Jwt:Key"]!;

        // Validar token firmado por email
        if (!TryValidateAccountActivationToken(token, secret, out string tokenEmail, out _))
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

        tokenEmail = (tokenEmail ?? "").Trim().ToLowerInvariant();

        if (!string.Equals(tokenEmail, email, StringComparison.Ordinal))
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "El token no corresponde al correo indicado.",
                data = null,
                statusCode = 400
            });
        }

        var proveedor = await _db.Proveedores
            .FirstOrDefaultAsync(p =>
                p.CorreoContacto != null &&
                p.CorreoContacto.ToLower() == email &&
                !p.IsDeleted);

        if (proveedor == null)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Proveedor inválido.",
                data = null,
                statusCode = 400
            });
        }

        var newHash = _passwordHasher.HashLegacy(newPassword);

        // Validar que la nueva contraseña no sea igual a la actual
        if (string.Equals((proveedor.PasswordHash ?? "").Trim(), newHash, StringComparison.Ordinal))
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

        proveedor.PasswordHash = newHash;
        proveedor.PasswordSetAt = DateTime.UtcNow;
        proveedor.DateModified = DateTime.UtcNow;
        proveedor.ModifiedBy = "RESET_PASSWORD";

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

    // =========================================================
    // POST /api/Authenticate/ChangePassword
    // =========================================================

    /// <summary>
    /// Cambia contraseña de colaborador autenticado.
    /// </summary>
    [HttpPost("ChangePassword")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] ChangePasswordRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("IsAuthenticated: {Auth}", User.Identity?.IsAuthenticated);
            _logger.LogInformation("Claims: {Claims}",
                string.Join(" | ", User.Claims.Select(c => $"{c.Type}={c.Value}")));

            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Solicitud inválida: verifique los datos ingresados.",
                    data = null,
                    statusCode = 400,
                    errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });
            }

            var proveedorIdStr =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("ProveedorId");

            if (string.IsNullOrWhiteSpace(proveedorIdStr) || !int.TryParse(proveedorIdStr, out int proveedorId))
            {
                return Unauthorized(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "No autorizado. No se pudo identificar al proveedor desde el token.",
                    data = null,
                    statusCode = 401
                });
            }

            var proveedor = await _db.Proveedores
                .FirstOrDefaultAsync(p =>
                    p.ProveedorId == proveedorId &&
                    !p.IsDeleted);

            if (proveedor == null)
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

            var currentHash = _passwordHasher.HashLegacy(model.OldPassword ?? "");

            if (!string.Equals((proveedor.PasswordHash ?? "").Trim(), currentHash, StringComparison.Ordinal))
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

            if (string.Equals((model.OldPassword ?? "").Trim(), (model.NewPassword ?? "").Trim(), StringComparison.Ordinal))
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "La nueva contraseña no puede ser igual a la actual.",
                    data = null,
                    statusCode = 400,
                    errors = new List<string> { "La nueva contraseña no puede ser igual a la actual." }
                });
            }

            proveedor.PasswordHash = _passwordHasher.HashLegacy(model.NewPassword ?? "");
            proveedor.PasswordSetAt = DateTime.UtcNow;
            proveedor.DateModified = DateTime.UtcNow;
            proveedor.ModifiedBy = "CHANGE_PASSWORD";

            await _db.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Contraseña actualizada con éxito.",
                data = null,
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar contraseña del proveedor.");

            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al cambiar la contraseña.",
                data = null,
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }

    // =========================================================
    // POST /api/Authenticate/SetProveedorPassword
    // =========================================================

    /// <summary>
    /// Establece la contraseña del proveedor con token de activación (JWT o HMAC).
    ///
    /// - Valida token.
    /// - Verifica que el email del token coincida con el email del request.
    /// - Busca proveedor por ProveedorId (proyección para evitar errores por NULL).
    /// - Guarda PasswordHash y PasswordSetAt.
    /// </summary>
    [HttpPost("SetProveedorPassword")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> SetProveedorPassword([FromBody] SetProveedorPasswordRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Solicitud inválida.",
                statusCode = 400
            });
        }

        var email = (model.Email ?? "").Trim().ToLowerInvariant();
        var password = (model.Password ?? "").Trim();
        var token = (model.Token ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Email, Password y Token son obligatorios.",
                statusCode = 400
            });
        }

        // Validar token (JWT o HMAC)
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
                    statusCode = 401
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
                    statusCode = 401
                });
            }
        }
        else
        {
            var secret = _config["Jwt:Key"]!;
            tokenOk = TryValidateAccountActivationToken(token, secret, out tokenEmail, out _);

            if (!tokenOk)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Token de activación inválido o expirado.",
                    statusCode = 401
                });
            }
        }

        if (!string.Equals(tokenEmail, email, StringComparison.Ordinal))
        {
            return Unauthorized(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "El email no coincide con el token.",
                statusCode = 401
            });
        }

        // Buscar proveedor SOLO por ProveedorId para evitar materializar filas con NULL
        var proveedorId = await _db.Proveedores
            .AsNoTracking()
            .Where(p =>
                p.CorreoContacto != null &&
                p.CorreoContacto.ToLower() == email &&
                !(p.IsDeleted))
            .Select(p => p.ProveedorId)
            .FirstOrDefaultAsync();

        if (proveedorId == 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Proveedor no encontrado.",
                statusCode = 400
            });
        }

        var hash = _passwordHasher.HashLegacy(password);

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE dbo.tb_Proveedores
            SET PasswordHash = {hash},
                PasswordSetAt = {DateTime.UtcNow},
                DateModified = {DateTime.UtcNow},
                ModifiedBy = 'PWD_SET'
            WHERE ProveedorId = {proveedorId};
        ");

        return Ok(new ApiResponse<object>
        {
            request_id = requestId,
            success = true,
            message = "Contraseña establecida correctamente.",
            statusCode = 200
        });
    }

    // =========================================================
    // Helpers: Password reset token (NO extra table)
    // Token = base64url(payload).base64url(hmac(payload))
    // payload = idEmpleado|expUtcTicks|passwordEncriptadoSnapshot
    // =========================================================
    private string CreatePasswordResetToken(AccesoUsuarioColaborador user, TimeSpan ttl)
    {
        var expTicks = DateTime.UtcNow.Add(ttl).Ticks;
        var snapshot = (user.PasswordEncriptado ?? "").Trim();
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

        if (!int.TryParse(fields[0], out idEmpleado) || idEmpleado <= 0) return false;

        if (!long.TryParse(fields[1], out expTicks)) return false;
        if (DateTime.UtcNow.Ticks > expTicks) return false;

        passwordEncriptadoSnapshot = (fields[2] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(passwordEncriptadoSnapshot)) return false;

        return true;
    }

    // =========================================================
    // Helpers: HMAC + Base64Url
    // =========================================================
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

    // =========================================================
    // Helpers: JWT
    // =========================================================
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
        var validTo = validFrom.AddHours(24);

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

    // =========================================================
    // Helpers: Account activation token (NO DB lookup)
    // Token = base64url(payload).base64url(hmac(payload))
    // payload = email|expUtcTicks
    // =========================================================
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