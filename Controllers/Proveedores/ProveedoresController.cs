using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado de la gestión pública de proveedores.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProveedoresController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NomclickDbContext _nomclickDb;

    /// <summary>
    /// Constructor con inyección de ambos contextos:
    /// - _db: base principal Velios
    /// - _nomclickDb: base secundaria Nomclick
    /// </summary>
    public ProveedoresController(AppDbContext db, NomclickDbContext nomclickDb)
    {
        _db = db;
        _nomclickDb = nomclickDb;
    }

    // =========================================================
    // POST /api/Proveedores/CreateProveedor
    // =========================================================
    [HttpPost("CreateProveedor")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateProveedor([FromBody] ProveedorCreateRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
            // =========================================================
            // 0) Validación básica del modelo
            // =========================================================
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

            // Normalización de datos de entrada
            var correo = (model.CorreoContacto ?? "").Trim().ToLowerInvariant();
            var rfc = (model.RFC ?? "").Trim();
            var razonSocial = (model.RazonSocial ?? "").Trim();
            var nombreComercial = (model.NombreComercial ?? "").Trim();
            var telefonoContacto = (model.TelefonoContacto ?? "").Trim();
            var representanteLegal = (model.RepresentanteLegal ?? "").Trim();

            // Campos de dirección
            var calle = (model.Calle ?? "").Trim();
            var codigoPostal = (model.CodigoPostal ?? "").Trim();
            var colonia = (model.Colonia ?? "").Trim();
            var delegacionMunicipio = (model.DelegacionMunicipio ?? "").Trim();
            var ciudad = (model.Ciudad ?? "").Trim();
            var estado = (model.Estado ?? "").Trim();
            var pais = (model.Pais ?? "").Trim();

            if (string.IsNullOrWhiteSpace(correo))
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "El correo de contacto es obligatorio.",
                    statusCode = 400
                });
            }

            // =========================================================
            // 1) Buscar proveedor existente en Velios
            // =========================================================
            var proveedorMin = await _db.Proveedores
                .AsNoTracking()
                .Where(p => p.CorreoContacto.ToLower() == correo && p.IsDeleted != true)
                .Select(p => new
                {
                    p.ProveedorId,
                    p.EstatusProveedorId,
                    p.CreatedBy,
                    p.DateCreated,
                    p.CorreoContacto,
                    p.IsDeleted,
                    p.PasswordHash,
                    p.PasswordSetAt
                })
                .FirstOrDefaultAsync();

            if (proveedorMin == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Debe solicitar primero el enlace de activación.",
                    statusCode = 400
                });
            }

            // =========================================================
            // 2) Validar que esté activo
            // =========================================================
            if (proveedorMin.EstatusProveedorId != 1)
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Debe activar su cuenta desde el enlace enviado al correo antes de continuar.",
                    statusCode = 400
                });
            }

            // =========================================================
            // 3) Validar RFC duplicado en Velios
            // =========================================================
            if (!string.IsNullOrWhiteSpace(rfc))
            {
                var existeRFC = await _db.Proveedores
                    .AsNoTracking()
                    .AnyAsync(p =>
                        p.ProveedorId != proveedorMin.ProveedorId &&
                        p.RFC == rfc &&
                        p.IsDeleted != true);

                if (existeRFC)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        request_id = requestId,
                        success = false,
                        message = "Ya existe un proveedor con ese RFC.",
                        statusCode = 400
                    });
                }
            }

            var fechaActual = DateTime.UtcNow;

            // =========================================================
            // 4) Actualizar en Velios
            // =========================================================
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE dbo.tb_Proveedores
                SET RFC = {rfc},
                    RazonSocial = {razonSocial},
                    NombreComercial = {nombreComercial},
                    TelefonoContacto = {telefonoContacto},
                    RepresentanteLegal = {representanteLegal},
                    Calle = {calle},
                    CodigoPostal = {codigoPostal},
                    Colonia = {colonia},
                    DelegacionMunicipio = {delegacionMunicipio},
                    Ciudad = {ciudad},
                    Estado = {estado},
                    Pais = {pais},
                    DateModified = {fechaActual},
                    ModifiedBy = {"PUBLIC"}
                WHERE ProveedorId = {proveedorMin.ProveedorId};
            ");

            // =========================================================
            // 5) Replicar en Nomclick
            //    Si ya existe: UPDATE
            //    Si no existe: INSERT
            // =========================================================
            var existeEnNomclick = await _nomclickDb.Proveedores
                .AsNoTracking()
                .AnyAsync(p => p.ProveedorId == proveedorMin.ProveedorId);

            if (existeEnNomclick)
            {
                // Si ya existe en Nomclick, solo actualizamos
                await _nomclickDb.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE dbo.tb_Proveedores
                    SET RFC = {rfc},
                        RazonSocial = {razonSocial},
                        NombreComercial = {nombreComercial},
                        CorreoContacto = {correo},
                        TelefonoContacto = {telefonoContacto},
                        RepresentanteLegal = {representanteLegal},
                        EstatusProveedorId = {proveedorMin.EstatusProveedorId},
                        Calle = {calle},
                        CodigoPostal = {codigoPostal},
                        Colonia = {colonia},
                        DelegacionMunicipio = {delegacionMunicipio},
                        Ciudad = {ciudad},
                        Estado = {estado},
                        Pais = {pais},
                        DateModified = {fechaActual},
                        ModifiedBy = {"PUBLIC"}
                    WHERE ProveedorId = {proveedorMin.ProveedorId};
                ");
            }
            else
            {
                // Si no existe en Nomclick, lo insertamos con el mismo ProveedorId
                await _nomclickDb.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO dbo.tb_Proveedores
                    (
                        ProveedorId,
                        RFC,
                        RazonSocial,
                        NombreComercial,
                        CorreoContacto,
                        TelefonoContacto,
                        RepresentanteLegal,
                        EstatusProveedorId,
                        CreatedBy,
                        ModifiedBy,
                        DateCreated,
                        DateModified,
                        IsDeleted,
                        PasswordHash,
                        PasswordSetAt,
                        Calle,
                        CodigoPostal,
                        Colonia,
                        DelegacionMunicipio,
                        Ciudad,
                        Estado,
                        Pais,
                        LogoUrl
                    )
                    VALUES
                    (
                        {proveedorMin.ProveedorId},
                        {rfc},
                        {razonSocial},
                        {nombreComercial},
                        {correo},
                        {telefonoContacto},
                        {representanteLegal},
                        {proveedorMin.EstatusProveedorId},
                        {proveedorMin.CreatedBy},
                        {"PUBLIC"},
                        {proveedorMin.DateCreated},
                        {fechaActual},
                        {proveedorMin.IsDeleted},
                        {proveedorMin.PasswordHash},
                        {proveedorMin.PasswordSetAt},
                        {calle},
                        {codigoPostal},
                        {colonia},
                        {delegacionMunicipio},
                        {ciudad},
                        {estado},
                        {pais},
                        {null}
                    );
                ");
            }

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Proveedor actualizado con éxito en Velios y Nomclick.",
                data = new { proveedorMin.ProveedorId },
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                request_id = requestId,
                success = false,
                message = "Error al registrar proveedor.",
                errors = new List<string> { ex.Message },
                statusCode = 400
            });
        }
    }
}