using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Services.CodigosPostales;
using velios.Api.Services.Email;
using velios.Api.Services.ProveedoresDocs;
using velios.Api.Services.Security;

var builder = WebApplication.CreateBuilder(args);

#region ============================= CONFIGURACIÓN DE SERVICIOS =============================

// SMTP settings (appsettings.json -> "Smtp")
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));

// Email sender
builder.Services.AddScoped<IEmailSender, BrevoSmtpEmailSender>();

// Códigos postales
builder.Services.AddScoped<ICodigoPostalService, CodigoPostalService>();

// Password hasher (legacy)
builder.Services.AddSingleton<IPasswordHasher, LegacyPasswordHasher>();

// Proveedor documentos
builder.Services.AddScoped<IProveedorDocumentService, ProveedorDocumentService>();

// Controllers
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("VeliosConnection")));

#endregion

#region ============================= CONFIGURACIÓN JWT =============================

// JWT Key
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new Exception("Jwt:Key missing");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            // tolerancia de tiempo para expiración
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

#endregion

var app = builder.Build();

#region ============================= PIPELINE HTTP =============================

// ✅ Middleware de diagnóstico: confirma si la request llega al API (antes de MVC)
app.Use(async (ctx, next) =>
{
    app.Logger.LogInformation("Incoming: {Method} {Path}{Query} CT={CT} Len={Len}",
        ctx.Request.Method,
        ctx.Request.Path,
        ctx.Request.QueryString,
        ctx.Request.ContentType,
        ctx.Request.ContentLength);
    await next();
});

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("LocalDev", p =>
        p.AllowAnyHeader()
         .AllowAnyMethod()
         .AllowAnyOrigin());
});

¿
// Swagger (actívalo siempre o solo en Development; tú decides)
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("LocalDevCors");     // 👈 aquí

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

#endregion
Console.WriteLine($"API started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
app.Run();