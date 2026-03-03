using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Proveedores;

namespace velios.Api.Controllers;

<<<<<<< HEAD
/// <summary>
/// Controlador encargado de la gestión pública de proveedores.
///
/// Flujo funcional:
/// 1) El proveedor solicita activación (correo).
/// 2) Activa su cuenta mediante token.
/// 3) Completa sus datos mediante este endpoint.
/// 
/// Este controlador:
/// - No crea proveedores desde cero.
/// - Solo actualiza registros previamente creados durante la activación.
/// - Evita materializar entidades incompletas para prevenir errores de NULL.
/// </summary>
=======
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
[ApiController]
[Route("api/[controller]")]
public class ProveedoresController : ControllerBase
{
    private readonly AppDbContext _db;

<<<<<<< HEAD
    /// <summary>
    /// Constructor con inyección de dependencia del DbContext.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
=======
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
    public ProveedoresController(AppDbContext db)
    {
        _db = db;
    }

<<<<<<< HEAD
    // =========================================================
    // POST /api/Proveedores/CreateProveedor
    // =========================================================

    /// <summary>
    /// Completa el registro público de un proveedor previamente activado.
    ///
    /// Flujo:
    /// 1) Valida que exista un registro previo (creado en activación).
    /// 2) Verifica que la cuenta esté activada (EstatusProveedorId = 1).
    /// 3) Valida que el RFC no esté duplicado.
    /// 4) Actualiza los datos mediante SQL directo (sin materializar entidad).
    ///
    /// Reglas importantes:
    /// - El proveedor debe haber solicitado y activado su cuenta.
    /// - No permite duplicar RFC activos.
    /// - Solo actualiza registros no eliminados (IsDeleted != true).
    /// </summary>
    /// <param name="model">Datos del proveedor a completar.</param>
    /// <returns>
    /// ApiResponse con:
    /// - success = true si se actualiza correctamente.
    /// - ProveedorId actualizado.
    /// </returns>
    /// <remarks>
    /// Este método usa proyección mínima (Select anónimo) para evitar el error:
    /// "Data is Null. This method or property cannot be called on Null values."
    /// 
    /// Se ejecuta UPDATE vía SQL para:
    /// - Evitar problemas de tracking.
    /// - Prevenir materialización de columnas obligatorias NULL.
    /// </remarks>
    // =========================================================
    // POST /api/Proveedores/CreateProveedor
    // =========================================================
=======
    // =========================
    // POST /api/Proveedores
    // Alta pública
    // =========================
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
    [HttpPost("CreateProveedor")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateProveedor([FromBody] ProveedorCreateRequest model)
    {
        var requestId = Guid.NewGuid().ToString();

        try
        {
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

            var correo = (model.CorreoContacto ?? "").Trim().ToLowerInvariant();
            var rfc = (model.RFC ?? "").Trim();
            var razonSocial = (model.RazonSocial ?? "").Trim();

<<<<<<< HEAD
            // ✅ Nuevos campos de dirección
            var calle = (model.Calle ?? "").Trim();
            var codigoPostal = (model.CodigoPostal ?? "").Trim();
            var colonia = (model.Colonia ?? "").Trim(); // OJO: antes decía "Fin de vigencia", aquí es Colonia
            var delegacionMunicipio = (model.DelegacionMunicipio ?? "").Trim();
            var ciudad = (model.Ciudad ?? "").Trim();
            var estado = (model.Estado ?? "").Trim();
            var pais = (model.Pais ?? "").Trim();

=======
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
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

<<<<<<< HEAD
            // =========================================================
            // 1) Validar que el proveedor exista (registro previo activación)
            // =========================================================
=======
            // 1) NO cargamos el entity completo (evita "Data is Null..." al materializar)
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
            var proveedorMin = await _db.Proveedores
                .AsNoTracking()
                .Where(p => p.CorreoContacto.ToLower() == correo && p.IsDeleted != true)
                .Select(p => new
                {
                    p.ProveedorId,
                    p.EstatusProveedorId
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

<<<<<<< HEAD
            // =========================================================
            // 2) Validar que esté ACTIVADO
            // =========================================================
=======
            // 2) Validar ACTIVADO
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
            if (proveedorMin.EstatusProveedorId != 1) // 1 = ACTIVO
            {
                return BadRequest(new ApiResponse<object>
                {
                    request_id = requestId,
                    success = false,
                    message = "Debe activar su cuenta desde el enlace enviado al correo antes de continuar.",
                    statusCode = 400
                });
            }

<<<<<<< HEAD
            // =========================================================
            // 3) Validar RFC duplicado
            // =========================================================
=======
            // 3) Validar RFC duplicado (si RFC viene)
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
            if (!string.IsNullOrWhiteSpace(rfc))
            {
                var existeRFC = await _db.Proveedores
                    .AsNoTracking()
<<<<<<< HEAD
                    .AnyAsync(p =>
                        p.ProveedorId != proveedorMin.ProveedorId &&
                        p.RFC == rfc &&
                        p.IsDeleted != true);
=======
                    .AnyAsync(p => p.ProveedorId != proveedorMin.ProveedorId
                                   && p.RFC == rfc
                                   && p.IsDeleted != true);
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59

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

<<<<<<< HEAD
            // =========================================================
            // 4) UPDATE vía SQL directo (incluye dirección)
            // =========================================================
=======
            // 4) UPDATE por SQL (sin materializar entidad)
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE dbo.tb_Proveedores
            SET RFC = {rfc},
                RazonSocial = {razonSocial},
                NombreComercial = {model.NombreComercial},
                TelefonoContacto = {model.TelefonoContacto},
                RepresentanteLegal = {model.RepresentanteLegal},
<<<<<<< HEAD

                Calle = {calle},
                CodigoPostal = {codigoPostal},
                Colonia = {colonia},
                DelegacionMunicipio = {delegacionMunicipio},
                Ciudad = {ciudad},
                Estado = {estado},
                Pais = {pais},

=======
>>>>>>> 9ea7874ac31375d8ad49080bcd0defe49c1bcd59
                DateModified = {DateTime.UtcNow},
                ModifiedBy = 'PUBLIC'
            WHERE ProveedorId = {proveedorMin.ProveedorId};
        ");

            return Ok(new ApiResponse<object>
            {
                request_id = requestId,
                success = true,
                message = "Proveedor actualizado con éxito.",
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