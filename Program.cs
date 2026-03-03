using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using velios.Api.Data;
using velios.Api.Models.Common;
<<<<<<< HEAD
using velios.Api.Services.CodigosPostales;
using velios.Api.Services.Email;
using velios.Api.Services.ProveedoresDocs;
using velios.Api.Services.Security;

/// <summary>
/// Punto de entrada principal de la aplicación Velios API.
/// 
/// Configura:
/// - Servicios de infraestructura (DbContext, Email, JWT)
/// - Middleware HTTP (Swagger, Auth, HTTPS)
/// - Pipeline de ejecución
/// 
/// Implementa el modelo de hosting minimalista introducido en .NET 6.
/// </summary>
var builder = WebApplication.CreateBuilder(args);

#region ============================= CONFIGURACIÓN DE SERVICIOS =============================

/// <summary>
/// Configuración del servicio SMTP para envío de correos.
/// Se enlaza con la sección "Smtp" de appsettings.json.
/// </summary>
builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));

/// <summary>
/// Registro del servicio de envío de correos.
/// Implementa el patrón de Inversión de Dependencias (DIP).
/// </summary>
builder.Services.AddScoped<IEmailSender, BrevoSmtpEmailSender>();

/// <summary>
/// Registro del servicio de codigos postales.
/// Implementa el patrón de Inversión de Dependencias (DIP).
/// </summary>
builder.Services.AddScoped<ICodigoPostalService, CodigoPostalService>();

/// <summary>
/// Registro del servicio de semilla de password
/// Implementa el patrón de Inversión de Dependencias (DIP).
/// </summary>
builder.Services.AddSingleton<IPasswordHasher, LegacyPasswordHasher>();

/// <summary>
/// Registro de controladores MVC.
/// </summary>
builder.Services.AddControllers();

builder.Services.AddScoped<IProveedorDocumentService, ProveedorDocumentService>();

/// <summary>
/// Registro de servicios para Swagger / OpenAPI.
/// </summary>
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

/// <summary>
/// Configuración del DbContext principal.
/// 
/// Conecta con SQL Server usando la cadena:
/// "VeliosConnection" definida en appsettings.json.
/// </summary>
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("VeliosConnection")));

#endregion

#region ============================= CONFIGURACIÓN JWT =============================

/// <summary>
/// Clave secreta utilizada para firmar y validar tokens JWT.
/// Debe almacenarse de forma segura (variables de entorno o KeyVault).
/// </summary>
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new Exception("Jwt:Key missing");

/// <summary>
/// Configuración del esquema de autenticación JWT Bearer.
/// 
/// Valida:
/// - Issuer
/// - Audience
/// - Firma
/// - Expiración
/// </summary>
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
=======
using velios.Api.Services.Email;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailSender, BrevoSmtpEmailSender>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ DbContext: apunta a la BD donde está tb_AccesoUsuariosColaborador
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("VeliosConnection")));

// ✅ JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
<<<<<<< HEAD

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),

            // Permite una pequeña tolerancia de tiempo para expiración
=======
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

<<<<<<< HEAD
/// <summary>
/// Habilita el sistema de autorización basado en roles o políticas.
/// </summary>
builder.Services.AddAuthorization();

#endregion

var app = builder.Build();

#region ============================= PIPELINE HTTP =============================

/// <summary>
/// Habilita Swagger para documentación y pruebas de API.
/// En producción puede restringirse a entorno Development.
/// </summary>
=======
builder.Services.AddAuthorization();

var app = builder.Build();

>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

<<<<<<< HEAD
/// <summary>
/// Fuerza redirección HTTPS.
/// </summary>
app.UseHttpsRedirection();

/// <summary>
/// Habilita autenticación JWT.
/// Debe ir antes de UseAuthorization().
/// </summary>
app.UseAuthentication();

/// <summary>
/// Habilita autorización basada en atributos [Authorize].
/// </summary>
app.UseAuthorization();

/// <summary>
/// Mapea los endpoints de los controladores.
/// </summary>
app.MapControllers();

#endregion

/// <summary>
/// Inicia la aplicación.
/// </summary>
=======
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
app.Run();