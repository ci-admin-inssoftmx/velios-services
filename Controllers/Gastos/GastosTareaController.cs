using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Tareas.Requests;
using velios.Api.Utils;
using velios.Api.Models.Notificaciones;
using velios.Api.Models.Tareas;
using velios.Api.Controllers.Tareas;


namespace velios.Api.Controllers;

[ApiController]
[Route("api/GastosTarea")]
public class GastosTareaController : ControllerBase
{
    private readonly AppDbContext _db;

    public GastosTareaController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Registra un gasto para una tarea y actualiza el presupuesto disponible y usado.
    /// Todo el proceso se ejecuta dentro de una transacción SQL.
    /// </summary>
    [HttpPost("Guardar")]
    public async Task<ActionResult<ApiResponse<object>>> Guardar([FromBody] GastoTareaRequest model)
    {
        // Validaciones básicas
        if (model.IdTarea <= 0 || model.Gasto <= 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "IdTarea y Gasto son requeridos y deben ser mayores a 0.",
                statusCode = 400
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 1. Buscar la tarea
            var tarea = await _db.Tareas.FirstOrDefaultAsync(x => x.TareaId == model.IdTarea && !x.IsDeleted);

            if (tarea == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    success = false,
                    message = "Tarea no encontrada.",
                    statusCode = 404
                });
            }

            if (tarea.PresupuestoAsignado == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    success = false,
                    message = "La tarea no tiene presupuesto asignado.",
                    statusCode = 400
                });
            }


            var presupuestoFinal = tarea.PresupuestoFinalAutorizado == null
                ? tarea.PresupuestoAsignado!.Value
                : tarea.PresupuestoFinalAutorizado!.Value;



            // 1. Obtener el presupuesto disponible actual.
            //    Si no existe, se utiliza el presupuesto asignado original.
            var presupuestoDisponible =
                tarea.PresupuestoDisponible == null
                ? presupuestoFinal
                : tarea.PresupuestoDisponible;

            // 2. Calcular cómo quedaría el presupuesto si se aplica el gasto.
            var presupuestoDisponibleConGasto =
                presupuestoDisponible - model.Gasto;

            // 3. Determinar si el gasto excede el presupuesto.
            var excedePresupuesto =
                presupuestoDisponibleConGasto < 0;

            // 4. Si excede el presupuesto, el gasto queda pendiente
            //    y no debe afectar el presupuesto disponible.
            if (!excedePresupuesto)
            {
                presupuestoDisponible = presupuestoDisponibleConGasto;
            }

            // 5. Calcular el presupuesto utilizado real.
            var presupuestoUsado =
                presupuestoFinal - presupuestoDisponible;


            // 5. Actualizar tb_Tareas
            tarea.PresupuestoDisponible = presupuestoDisponible;
            tarea.PresupuestoUsado = presupuestoUsado;
            tarea.DateModified = DateTime.UtcNow;

            // 6. Insertar el gasto en tb_GastosTarea
            var fechaRegistro = DateTime.Now; // ← NUEVO


            var idEstatusGasto = await GetIdEstatusGasto(
                Constants.EstatusGasto.SIN_RESPUESTA);


            var minutosSegundaNotificacion =
            await GetMinutosSegundaNotificacionAsync();


            var gastoGuardado = new GastoTarea
            {
                IdTarea = model.IdTarea,
                Gasto = model.Gasto,
                FechaRegistro = fechaRegistro,
                Descripcion = model.Descripcion?.Trim(),      // ← NUEVO
                RegisteredById = model.RegisteredById,           // ← NUEVO
                RegisteredByType = model.RegisteredByType?.Trim(),  // ← NUEVO
                ExecedePresupuesto = excedePresupuesto,// ← NUEVO
                IdCatEstatusGasto = excedePresupuesto ? idEstatusGasto : null // ← 3 = SIN_RESPUESTA

            };

            _db.GastosTarea.Add(gastoGuardado);
            await _db.SaveChangesAsync();


            var gasto = await ObtenerGastoAsync(
                gastoGuardado.IdGastoTarea,
                idEstatusGasto,
                minutosSegundaNotificacion);

            if (excedePresupuesto)
            {
                //Si el presupuestp disponible es negativo
                //Se procede a insertar una notificación para enviar al cliente
                var notificacion = await GetBudgetExceededNotification(gastoGuardado, fechaRegistro);
                _db.Notificaciones.Add(notificacion);
                await _db.SaveChangesAsync();
            }


            await transaction.CommitAsync();

            return Ok(new ApiResponse<object>
            {
                success = true,
                message = "Gasto registrado correctamente.",
                statusCode = 200,
                data = new
                {
                    tarea.PresupuestoAsignado,
                    PresupuestoDisponible = presupuestoDisponible,
                    PresupuestoUsado = presupuestoUsado,
                    tarea.PresupuestoFinalAutorizado,
                    fechaRegistro,// ← NUEVO
                    gasto
                }
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new ApiResponse<object>
            {
                success = false,
                message = "Error al registrar el gasto.",
                statusCode = 400,
                errors = new List<string> { ex.Message }
            });
        }
    }



    /// <summary>
    /// Construye una notificación de presupuesto excedido asociada al gasto registrado.
    /// La notificación se crea con estado pendiente y se configura como el primer intento
    /// de envío.
    /// </summary>
    /// <param name="gastoGuardado">
    /// Gasto de tarea previamente registrado. Su identificador se utiliza como referencia
    /// de la notificación.
    /// </param>
    /// <param name="fechaRegistro">
    /// Fecha y hora en la que se registra la notificación.
    /// </param>
    /// <returns>
    /// Una instancia de <see cref="Notificaciones"/> configurada para notificar
    /// el excedente de presupuesto.
    /// </returns>
    private async Task<Notificaciones> GetBudgetExceededNotification(GastoTarea gastoGuardado, DateTime fechaRegistro)
    {
        var idTipoNotificacion = await GetIdTipoNotificacion(Constants.TipoNotificacionCodigo.PRESUPUESTO_EXCEDIDO);
        var idEstatusNotificacion = await GetIdEstatusNotificacion(Constants.EstatusNotificacionCodigo.PENDIENTE);


        return new Notificaciones
        {
            IdTipoNotificacion = idTipoNotificacion,
            IdCatEstatusNotificacion = idEstatusNotificacion,
            IdReferencia = gastoGuardado.IdGastoTarea,
            TablaReferencia = "tb_GastosTarea",
            Intento = 1,//PRIMER INTENTO
            NumeroReintentos = 0, //
            FechaRegistro = fechaRegistro
        };

    }



    /// <summary>
    /// Obtiene el identificador del tipo de notificación asociado al código proporcionado.
    /// </summary>
    /// <param name="codigo">
    /// Código del evento utilizado para identificar el tipo de notificación.
    /// </param>
    /// <returns>
    /// Identificador del tipo de notificación.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando no existe un tipo de notificación asociado al código proporcionado.
    /// </exception>
    private async Task<int> GetIdTipoNotificacion(string codigo)
    {
        var tipoNotificacion = await _db.CatTipoNotificacion
            .FirstOrDefaultAsync(x => x.Evento == codigo);

        if (tipoNotificacion == null)
        {
            throw new InvalidOperationException(
                $"No existe un tipo de notificación con el código '{codigo}'."
            );
        }

        return tipoNotificacion.IdTipoNotificacion;
    }


    /// <summary>
    /// Obtiene el identificador del estatus de notificación asociado al código proporcionado.
    /// </summary>
    /// <param name="codigo">
    /// Código del estatus utilizado para identificar el registro correspondiente.
    /// </param>
    /// <returns>
    /// Identificador del estatus de notificación.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando no existe un estatus de notificación asociado al código proporcionado.
    /// </exception>
    private async Task<int> GetIdEstatusNotificacion(string codigo)
    {
        var estatusNotificacion = await _db.CatEstatusNotificacion
            .FirstOrDefaultAsync(x => x.Estatus == codigo);

        if (estatusNotificacion == null)
        {
            throw new InvalidOperationException(
                $"No existe un estatus de notificación con el código '{codigo}'."
            );
        }

        return estatusNotificacion.IdCatEstatusNotificacion;
    }


    /// <summary>
    /// Obtiene el identificador del estatus de gasto asociado al código proporcionado.
    /// </summary>
    /// <param name="codigo">
    /// Código del estatus utilizado para identificar el registro correspondiente.
    /// </param>
    /// <returns>
    /// Identificador del estatus de gasto.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando no existe un estatus de gasto asociado al código proporcionado.
    /// </exception>
    private async Task<int> GetIdEstatusGasto(string codigo)
    {
        var estatusGasto = await _db.CatEstatusGasto
            .FirstOrDefaultAsync(x => x.Estatus == codigo);

        if (estatusGasto == null)
        {
            throw new InvalidOperationException(
                $"No existe un estatus de notificación con el código '{codigo}'."
            );
        }
        return estatusGasto.IdCatEstatusGasto;
    }

    /// <summary>
    /// Obtiene la cantidad de minutos configurada para determinar el tiempo de espera
    /// antes de enviar una segunda notificación.
    /// </summary>
    /// <returns>
    /// Cantidad de minutos configurada para la segunda notificación.
    /// Si la configuración no existe o su valor no es válido, se devuelve el valor
    /// determinado por <see cref="GetConfigInt(string)"/>.
    /// </returns>
    private async Task<int> GetMinutosSegundaNotificacionAsync()
    {
        var configuracion = await _db.ConfiguracionNotificacion
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Clave == Constants.ConfiguracionNotificacion
                    .TIEMPO_SEGUNDA_NOTIFICACION_MINUTOS);

        return GetConfigInt(configuracion?.Valor);
    }


    /// <summary>
    /// Convierte el valor de una configuración a un número entero válido.
    /// </summary>
    /// <param name="valor">
    /// Valor de configuración que se desea convertir.
    /// </param>
    /// <returns>
    /// Número entero obtenido a partir del valor proporcionado.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando el valor no es un número entero válido o es menor o igual a cero.
    /// </exception>
    private int GetConfigInt(string? valor)
    {
        if (!int.TryParse(valor, out var resultado) || resultado <= 0)
        {
            throw new InvalidOperationException(
                $"La configuración '{valor}' debe ser un número entero mayor a cero.");
        }

        return resultado;
    }


    /// <summary>
    /// Obtiene la información de un gasto de tarea y determina si cumple las condiciones
    /// para generar una segunda notificación por exceder el presupuesto y superar el
    /// tiempo de respuesta configurado.
    /// </summary>
    /// <param name="idGasto">
    /// Identificador del gasto de tarea que se desea consultar.
    /// </param>
    /// <param name="idEstatusGastoSinRespuesta">
    /// Identificador del estatus que representa un gasto sin respuesta del cliente.
    /// </param>
    /// <param name="minutosSegundaNotificacion">
    /// Cantidad de minutos que deben transcurrir desde el registro del gasto
    /// para considerar que excedió el tiempo de respuesta.
    /// </param>
    /// <returns>
    /// Un objeto con la información del gasto, los datos del usuario que lo registró,
    /// su estatus y la validación de tiempo para una segunda notificación.
    /// Devuelve <see langword="null"/> si no se encuentra el gasto indicado.
    /// </returns>
    private async Task<object?> ObtenerGastoAsync(
    int idGasto,
    int idEstatusGastoSinRespuesta,
    int minutosSegundaNotificacion)
    {
        var fechaLimite = DateTime.Now.AddMinutes(
            -minutosSegundaNotificacion);

        return await _db.GastosTarea
            .AsNoTracking()
            .Where(g => g.IdGastoTarea == idGasto)
            .Select(g => new
            {
                idGasto = g.IdGastoTarea,
                idTarea = g.IdTarea,
                gasto = g.Gasto,
                fechaRegistro = g.FechaRegistro,
                descripcion = g.Descripcion,

                registeredById = g.RegisteredById,
                registeredByType = g.RegisteredByType,

                nombreUsuario =
                    g.RegisteredByType == "Proveedor"
                        ? _db.Proveedores
                            .Where(p => p.ProveedorId == g.RegisteredById)
                            .Select(p => p.NombreComercial)
                            .FirstOrDefault()

                        : g.RegisteredByType == "Trabajador"
                            ? _db.ProveedorTrabajadores
                                .Where(t =>
                                    t.TrabajadorId == (long)g.RegisteredById)
                                .Select(t =>
                                    (t.Nombre + " " +
                                     t.ApellidoPaterno + " " +
                                     t.ApellidoMaterno).Trim())
                                .FirstOrDefault()

                            : null,

                execedePresupuesto = g.ExecedePresupuesto,

                idCatEstatusGasto = g.IdCatEstatusGasto,

                descripcionEstatus =
                    _db.CatEstatusGasto
                        .Where(p =>
                            g.IdCatEstatusGasto.HasValue &&
                            p.IdCatEstatusGasto ==
                            g.IdCatEstatusGasto.Value)
                        .Select(p => p.Estatus)
                        .FirstOrDefault() ?? string.Empty,

                fechaClienteRespuestaGasto =
                    g.FechaClienteRespuestaGasto,

                excedeTiempoRespuesta =
                    g.IdCatEstatusGasto == idEstatusGastoSinRespuesta &&
                    g.ExecedePresupuesto == true &&
                    g.FechaRegistro <= fechaLimite,

                minutosSegundaNotificacion
            })
            .FirstOrDefaultAsync();
    }


}