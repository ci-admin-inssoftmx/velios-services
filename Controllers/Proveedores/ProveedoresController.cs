using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

/// <summary>
/// Controlador encargado de la gestión pública de proveedores.
/// 
/// Funcionalidades principales:
/// - Completar el registro de un proveedor previamente creado.
/// - Actualizar la información fiscal, comercial y de contacto.
/// - Replicar la información en la base secundaria Nomclick.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProveedoresController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NomclickDbContext _nomclickDb;

    /// <summary>
    /// Inicializa una nueva instancia del controlador de proveedores.
    /// </summary>
    /// <param name="db">Contexto principal de la base de datos Velios.</param>
    /// <param name="nomclickDb">Contexto secundario de la base de datos Nomclick.</param>
    public ProveedoresController(AppDbContext db, NomclickDbContext nomclickDb)
    {
        _db = db;
        _nomclickDb = nomclickDb;
    }

    // =========================================================
    // POST /api/Proveedores/CreateProveedor
    // =========================================================

    /// <summary>
    /// Completa el registro de un proveedor previamente creado en el sistema,
    /// actualizando sus datos fiscales, comerciales, de contacto, dirección
    /// y geolocalización.
    /// 
    /// Flujo general:
    /// 1. Valida el modelo recibido.
    /// 2. Normaliza los datos de entrada.
    /// 3. Busca al proveedor existente por correo en Velios.
    /// 4. Verifica que la cuenta esté activa.
    /// 5. Valida que el RFC no esté duplicado.
    /// 6. Actualiza la información en Velios.
    /// 7. Replica la información en Nomclick:
    ///    - Si ya existe, actualiza.
    ///    - Si no existe, inserta.
    /// </summary>
    /// <param name="model">Datos de actualización del proveedor.</param>
    /// <returns>
    /// Respuesta estándar de la API con el identificador del proveedor actualizado.
    /// </returns>
    [HttpPost("CreateProveedor")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateProveedor([FromBody] ProveedorCreateRequest model)
    {
        

        try
        {
            // =========================================================
            // 0) Validación básica del modelo
            // =========================================================
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Solicitud inválida.",
                    statusCode = 400
                });
            }



            // =========================================================
            // 1) Normalización de datos de entrada
            // =========================================================
            var correo = (model.CorreoContacto ?? "").Trim().ToLowerInvariant();

            // Se convierten cadenas vacías a null para evitar guardar ""
            var rfc = string.IsNullOrWhiteSpace(model.RFC) ? null : model.RFC.Trim();
            var razonSocial = string.IsNullOrWhiteSpace(model.RazonSocial) ? null : model.RazonSocial.Trim();
            var nombreComercial = string.IsNullOrWhiteSpace(model.NombreComercial) ? null : model.NombreComercial.Trim();
            var telefonoContacto = string.IsNullOrWhiteSpace(model.TelefonoContacto) ? null : model.TelefonoContacto.Trim();
            var representanteLegal = string.IsNullOrWhiteSpace(model.RepresentanteLegal) ? null : model.RepresentanteLegal.Trim();

            // Coordenadas geográficas
            var latitud = model.Latitud;
            var longitud = model.Longitud;

            // Campos de dirección
            var calle = string.IsNullOrWhiteSpace(model.Calle) ? null : model.Calle.Trim();
            var codigoPostal = string.IsNullOrWhiteSpace(model.CodigoPostal) ? null : model.CodigoPostal.Trim();
            var colonia = string.IsNullOrWhiteSpace(model.Colonia) ? null : model.Colonia.Trim();
            var delegacionMunicipio = string.IsNullOrWhiteSpace(model.DelegacionMunicipio) ? null : model.DelegacionMunicipio.Trim();
            var ciudad = string.IsNullOrWhiteSpace(model.Ciudad) ? null : model.Ciudad.Trim();
            var estado = string.IsNullOrWhiteSpace(model.Estado) ? null : model.Estado.Trim();
            var pais = string.IsNullOrWhiteSpace(model.Pais) ? null : model.Pais.Trim();




            if (string.IsNullOrWhiteSpace(correo))
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "El correo de contacto es obligatorio.",
                    statusCode = 400
                });
            }

            // =========================================================
            // 2) Validar correo duplicado en Trabajadores en Velios
            // =========================================================
            if (!string.IsNullOrWhiteSpace(correo))
            {
                var existeCorreo = await _db.ProveedorTrabajadores
                    .AsNoTracking()
                    .AnyAsync(p =>
                        p.Correo == correo);

                if (existeCorreo)
                {
                    return BadRequest(new ApiResponse<object>
                    {

                        success = false,
                        message = "Ya existe un registro con ese correo.",
                        statusCode = 400
                    });
                }
            }

            // =========================================================
            // 3) Validación de coordenadas
            // =========================================================
            if (latitud.HasValue && (latitud < -90 || latitud > 90))
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "La latitud es inválida. Debe estar entre -90 y 90.",
                    statusCode = 400
                });
            }

            if (longitud.HasValue && (longitud < -180 || longitud > 180))
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "La longitud es inválida. Debe estar entre -180 y 180.",
                    statusCode = 400
                });
            }

            // =========================================================
            // 4) Buscar proveedor existente en Velios
            // =========================================================
            var proveedorMin = await _db.Proveedores
                .AsNoTracking()
                .Where(p =>
                    p.CorreoContacto != null &&
                    p.CorreoContacto.ToLower() == correo &&
                    !p.IsDeleted)
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
                    
                    success = false,
                    message = "Debe solicitar primero el enlace de activación.",
                    statusCode = 400
                });
            }

            // =========================================================
            // 5) Validar que el proveedor esté activo
            // =========================================================
            if (proveedorMin.EstatusProveedorId != 1)
            {
                return BadRequest(new ApiResponse<object>
                {
                    
                    success = false,
                    message = "Debe activar su cuenta desde el enlace enviado al correo antes de continuar.",
                    statusCode = 400
                });
            }

            // =========================================================
            // 6) Validar RFC duplicado en Velios
            // =========================================================
            if (!string.IsNullOrWhiteSpace(rfc))
            {
                var existeRFC = await _db.Proveedores
                    .AsNoTracking()
                    .AnyAsync(p =>
                        p.ProveedorId != proveedorMin.ProveedorId &&
                        p.RFC == rfc &&
                        !p.IsDeleted);

                if (existeRFC)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        
                        success = false,
                        message = "Ya existe un proveedor con ese RFC.",
                        statusCode = 400
                    });
                }
            }

            var fechaActual = DateTime.UtcNow;

            // =========================================================
            // 7) Actualizar en Velios
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
                    Latitud = {latitud},
                    Longitud = {longitud},
                    DateModified = {fechaActual},
                    ModifiedBy = {"PUBLIC"}
                WHERE ProveedorId = {proveedorMin.ProveedorId};
            ");

            // =========================================================
            // 8) Replicar en Nomclick
            //    Si ya existe: UPDATE
            //    Si no existe: INSERT
            // =========================================================
            var existeEnNomclick = await _nomclickDb.Proveedores
                .AsNoTracking()
                .AnyAsync(p => p.ProveedorId == proveedorMin.ProveedorId);

            if (existeEnNomclick)
            {
                // -----------------------------------------------------
                // 8.1) Actualizar proveedor existente en Nomclick
                // -----------------------------------------------------
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
                        Latitud = {latitud},
                        Longitud = {longitud},
                        DateModified = {fechaActual},
                        ModifiedBy = {"PUBLIC"}
                    WHERE ProveedorId = {proveedorMin.ProveedorId};
                ");
            }
            else
            {
                // -----------------------------------------------------
                // 8.2) Insertar proveedor nuevo en Nomclick
                //      conservando el mismo ProveedorId
                // -----------------------------------------------------
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
                        Latitud,
                        Longitud,
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
                        {latitud},
                        {longitud},
                        {null}
                    );
                ");
            }

            // =========================================================
            // 9) Respuesta exitosa
            // =========================================================
            return Ok(new ApiResponse<object>
            {
                
                success = true,
                message = "Proveedor actualizado con éxito en Velios y Nomclick.",
                data = new
                {
                    proveedorMin.ProveedorId,
                    Latitud = latitud,
                    Longitud = longitud
                },
                statusCode = 200
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                
                success = false,
                message = "Error al registrar proveedor.",
                errors = new List<string> { ex.Message },
                statusCode = 400
            });
        }
    }
}