using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net.NetworkInformation;
using velios.Api.Data;
using velios.Api.Models.Common;
using velios.Api.Models.Empleado;
using velios.Api.Models.Notificaciones;
using velios.Api.Models.Tareas;
using velios.Api.Services.Email;
using velios.Api.Utils;


namespace velios.Api.Services.BackgroundServices
{
    public class NotificacionesBackgroundService : BackgroundService
    {
        //IServiceScopeFactory para poder crear un DbContext y consultar SQL Server.
        //No debemos inyectar directamente el DbContext en el BackgroundService.
        private readonly IServiceScopeFactory _scopeFactory;

        //ILogger para registrar mensajes y errores en los logs.
        private readonly ILogger<NotificacionesBackgroundService> _logger;


        private readonly IConfiguration _configuration;

        public NotificacionesBackgroundService(
           IServiceScopeFactory scopeFactory,
           ILogger<NotificacionesBackgroundService> logger,
           IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificacionesBackgroundService iniciado.");

            //Mientras la aplicación no solicite detener el servicio, sigue ejecutando el ciclo.
            // Cuando detengas tu API desde Visual Studio:
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Iniciando revisión de notificaciones pendientes.");

                    await ProcesarNotificacionesAsync(stoppingToken);


                    _logger.LogInformation("Iniciando revisión de notificaciones sin respuesta.");
                    // Revisar notificaciones enviadas hace 24 horas.
                    await ProcesarSegundasNotificacionesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ocurrió un error durante la revisión de notificaciones.");
                }

                try
                {
                    // Esperar 1 minuto antes de volver a revisar
                    //Mientras estamos desarrollando, puedes usar temporalmente:TimeSpan.FromSeconds(10), 10 segundo o el parametro deseado
                    await Task.Delay(
                        TimeSpan.FromMinutes(1),
                        stoppingToken
                    );
                }
                catch (OperationCanceledException)
                {
                    // La aplicación se está cerrando.
                    break;
                }
            }

            _logger.LogInformation("NotificacionesBackgroundService detenido.");
        }


        private async Task ProcesarNotificacionesAsync(
            CancellationToken stoppingToken)
        {
            // En cada ejecución del proceso creamos un contexto nuevo.
            using var scope = _scopeFactory.CreateScope();

            // Obtener dependencias necesarias.
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var nomclickDb = scope.ServiceProvider
                .GetRequiredService<NomclickDbContext>();





            var emailSender = scope.ServiceProvider
                .GetRequiredService<IEmailSender>();

            _logger.LogInformation(
                "Iniciando procesamiento de notificaciones."
            );


            var estatusPendiente =
                await GetRequiredEstatusNotificacionAsync(
                    db,
                    Constants.EstatusNotificacionCodigo.PENDIENTE,
                    stoppingToken
                );

            var estatusProcesando =
                await GetRequiredEstatusNotificacionAsync(
                    db,
                    Constants.EstatusNotificacionCodigo.PROCESANDO,
                    stoppingToken
                );

            var estatusEnviado =
                await GetRequiredEstatusNotificacionAsync(
                    db,
                    Constants.EstatusNotificacionCodigo.ENVIADO,
                    stoppingToken
                );

            var estatusError =
                await GetRequiredEstatusNotificacionAsync(
                    db,
                    Constants.EstatusNotificacionCodigo.ERROR,
                    stoppingToken
                );



            if (estatusPendiente == null)
            {
                _logger.LogError(
                    "No se encontró el estatus de notificación PENDIENTE."
                );

                return;
            }


            if (estatusProcesando == null)
            {
                _logger.LogError(
                    "No se encontró el estatus de notificación PROCESANDO."
                );

                return;
            }

            if (estatusEnviado == null)
            {
                _logger.LogError(
                    "No se encontró el estatus de notificación ENVIADO."
                );

                return;
            }

            if (estatusError == null)
            {
                _logger.LogError(
                    "No se encontró el estatus de notificación ERROR."
                );

                return;
            }



            // Obtener el tipo de notificación.
            var tipoNotificacion = await db.CatTipoNotificacion
                .FirstOrDefaultAsync(
                    x => x.Evento ==
                        Constants.TipoNotificacionCodigo.PRESUPUESTO_EXCEDIDO,
                    stoppingToken
                );

            if (tipoNotificacion == null)
            {
                _logger.LogError(
                    "No se encontró el tipo de notificación PRESUPUESTO_EXCEDIDO."
                );

                return;
            }

            // Obtener el template activo.
            var templateExcedido = await db.CatTemplateNotificacion
                .FirstOrDefaultAsync(
                    x =>
                        x.Nombre ==
                            Constants.TipoNotificacionCodigo.PRESUPUESTO_EXCEDIDO
                        &&
                        x.Activo,
                    stoppingToken
                );

            if (templateExcedido == null)
            {
                _logger.LogError(
                    "No se encontró un template activo para PRESUPUESTO_EXCEDIDO."
                );

                return;
            }



            // Obtener el template activo.
            var templateRemiender = await db.CatTemplateNotificacion
                .FirstOrDefaultAsync(
                    x =>
                        x.Nombre ==
                            Constants.TipoNotificacionCodigo.RECORDATORIO_PRESUPUESTO_EXCEDIDO
                        &&
                        x.Activo,
                    stoppingToken
                );

            if (templateRemiender == null)
            {
                _logger.LogError(
                    "No se encontró un template activo para RECORDATORIO_PRESUPUESTO_EXCEDIDO."
                );

                return;
            }



            var configuraciones = await GetNotificationConfigAsync(db, stoppingToken);


            var webClientesUrl = GetConfigString(
                configuraciones,
                Constants.ConfiguracionNotificacion
                    .WEB_CLIENTE_URL
            );

            var notificacionesPorLote = GetConfigInt(
                configuraciones,
                Constants.ConfiguracionNotificacion
                    .CANTIDAD_NOTIFICACIONES_LOTE
            );




            // Obtener las notificaciones pendientes.
            var notificacionesPendientes = await db.Notificaciones
                .Where(x =>
                    x.IdCatEstatusNotificacion ==
                        estatusPendiente.IdCatEstatusNotificacion
                    &&
                    x.IdTipoNotificacion ==
                        tipoNotificacion.IdTipoNotificacion
                )
                .OrderBy(x => x.FechaRegistro)
                .Take(notificacionesPorLote)
                .ToListAsync(stoppingToken);

            _logger.LogInformation(
                "Se encontraron {Cantidad} notificaciones pendientes.",
                notificacionesPendientes.Count
            );



            if (notificacionesPendientes.Count == 0)
            {
                _logger.LogInformation(
                    "No se encontraron notificaciones pendientes."
                );

                return;
            }



            var idsGastos = notificacionesPendientes
               .Where(x => x.TablaReferencia == Constants.TablaReferencia.tb_GastosTarea)
               .Select(x => x.IdReferencia)
               .Distinct()
               .ToList();

            if (idsGastos.Count == 0)
            {
                _logger.LogWarning(
                    "No se encontraron referencias de gastos en las notificaciones pendientes."
                );

                return;
            }

            //Información de gastos con notificaciones pendientes
            var gastos = await (
                    from gasto in db.GastosTarea
                    join tarea in db.Tareas
                        on gasto.IdTarea equals tarea.TareaId

                    where idsGastos.Contains(gasto.IdGastoTarea)

                    select new GastoNotificacionDto
                    {
                        IdGastoTarea = gasto.IdGastoTarea,
                        IdTarea = tarea.TareaId,

                        Gasto = gasto.Gasto,
                        Descripcion = gasto.Descripcion,
                        FechaRegistro = gasto.FechaRegistro,

                        TituloTarea = tarea.Titulo,
                        DescripcionTarea = tarea.Descripcion,

                        PresupuestoOriginal = tarea.PresupuestoAsignado,
                        PresupuestoDisponible = tarea.PresupuestoDisponible,
                        PresupuestoUsado = tarea.PresupuestoUsado,
                        PresupuestoFinalAutorizado =
                            tarea.PresupuestoFinalAutorizado,

                        RegisteredById = gasto.RegisteredById,
                        RegisteredByType = gasto.RegisteredByType,

                        NombreUsuarioTarea =
                            gasto.RegisteredByType == "Proveedor"

                                ? db.Proveedores
                                    .Where(p =>
                                        p.ProveedorId == gasto.RegisteredById
                                    )
                                    .Select(p => p.NombreComercial)
                                    .FirstOrDefault()

                                : gasto.RegisteredByType == "Trabajador"

                                    ? db.ProveedorTrabajadores
                                        .Where(t =>
                                            t.TrabajadorId == gasto.RegisteredById
                                        )
                                        .Select(t =>
                                            (
                                                t.Nombre + " " +
                                                t.ApellidoPaterno + " " +
                                                t.ApellidoMaterno
                                            ).Trim()
                                        )
                                        .FirstOrDefault()

                                    : null
                    }
                )
                .AsNoTracking()
                .ToListAsync(stoppingToken);



            if (gastos.Count == 0)
            {
                _logger.LogWarning(
                    "No se encontro la información de gastos y tareas  en las notificaciones pendientes."
                );

                return;
            }



            var responsables = await GetSupervisorDataAsync(
                  nomclickDb,
                  stoppingToken,
                  gastos
              );


            if (responsables.Count == 0)
            {
                _logger.LogWarning(
                    "No se encontraron responsables para las tareas pendientes."
                );

                return;
            }



            var sucursalData = await GetSucursalDataAsync(nomclickDb,
                  stoppingToken,
                  gastos);


            if (sucursalData.Count == 0)
            {
                _logger.LogWarning(
                    "No se encontraron responsables para las tareas pendientes."
                );

                return;
            }

            var sucursalDataPorIdTarea = sucursalData.ToDictionary(x => x.TareaId);

            var responsablesPorIdTarea = responsables.ToDictionary(x => x.IdRegistro);

            var gastosPorId = gastos.ToDictionary(x => x.IdGastoTarea);

            var context = new ConfiguracionProcesamientoNotificacion
            {
                IdEstatusPendiente = estatusPendiente.IdCatEstatusNotificacion,
                IdEstatusProcesando = estatusProcesando.IdCatEstatusNotificacion,
                IdEstatusEnviado = estatusEnviado.IdCatEstatusNotificacion,
                IdEstatusError = estatusError.IdCatEstatusNotificacion,
                WebClientesUrl = webClientesUrl,
                Template = templateExcedido,
                TemplateReminder = templateRemiender


            };


            var maximoReintentos = GetConfigInt(
                   configuraciones,
                   Constants.ConfiguracionNotificacion
                       .MAXIMO_REINTENTOS
               );



            // Procesar cada notificación.
            foreach (var notificacion in notificacionesPendientes)
            {

                if (!gastosPorId.TryGetValue(
                    notificacion.IdReferencia,
                    out var gasto))
                {
                    _logger.LogError(
                        "No se encontró el gasto relacionado con la notificación {IdNotificacion}.",
                        notificacion.IdNotificacion
                    );

                    //la notificación se marca como error
                    await SetNotificationEstatusErrorAsync(
                        notificacion,
                        db,
                        stoppingToken,
                        estatusError
                        );


                    continue;
                }


                if (!responsablesPorIdTarea.TryGetValue(
                    gasto.IdTarea,
                    out var responsable))
                {
                    _logger.LogError(
                        "No se encontró el supervisor de la tarea {IdTarea}.",
                        gasto.IdTarea
                    );


                    //la notificación se marca como error
                    await SetNotificationEstatusErrorAsync(
                        notificacion,
                        db,
                        stoppingToken,
                        estatusError
                        );
                    continue;
                }


                if (!sucursalDataPorIdTarea.TryGetValue(
                    gasto.IdTarea,
                    out var sucursal))
                {
                    _logger.LogError(
                        "No se encontró el supervisor de la tarea {IdTarea}.",
                        gasto.IdTarea
                    );


                    //la notificación se marca como error
                    await SetNotificationEstatusErrorAsync(
                        notificacion,
                        db,
                        stoppingToken,
                        estatusError
                        );

                    continue;
                }



                await ProcesarNotificacionAsync(
                    db,
                    notificacion,
                    gasto,
                    stoppingToken,
                    emailSender,
                    responsable,
                    sucursal,
                    context

                );
            }
        }



        /// <summary>
        /// Procesa una notificación de presupuesto, construye el contenido del correo,
        /// actualiza el estado de la notificación e intenta enviarlo al responsable.
        /// En caso de error durante el envío, registra el reintento y determina si la
        /// notificación debe permanecer pendiente o pasar a estado de error.
        /// </summary>
        /// <param name="db">
        /// Contexto de base de datos utilizado para consultar y actualizar la notificación.
        /// </param>
        /// <param name="notificacion">
        /// Notificación que se está procesando, incluyendo su intento y estado actual.
        /// </param>
        /// <param name="gasto">
        /// Información del gasto asociada a la notificación.
        /// </param>
        /// <param name="stoppingToken">
        /// Token utilizado para cancelar las operaciones asíncronas de base de datos.
        /// </param>
        /// <param name="emailSender">
        /// Servicio encargado de realizar el envío del correo electrónico.
        /// </param>
        /// <param name="responsable">
        /// Responsable que recibirá la notificación. Su correo electrónico se valida
        /// antes de intentar realizar el envío.
        /// </param>
        /// <param name="sucursal">
        /// Información de la sucursal relacionada con el gasto y utilizada para construir
        /// el contenido del correo.
        /// </param>
        /// <param name="context">
        /// Configuración necesaria para procesar la notificación, incluyendo los
        /// identificadores de estados y la información del template.
        /// </param>
        private async Task ProcesarNotificacionAsync(
            AppDbContext db,
            Notificaciones notificacion,
            GastoNotificacionDto gasto,
            CancellationToken stoppingToken,
            IEmailSender emailSender,
            ResponsableNotificacionDto responsable,
            SucursalNotificacionDto sucursal,
            ConfiguracionProcesamientoNotificacion context)
        {
            try
            {


                if (string.IsNullOrWhiteSpace(responsable.Email))
                {
                    _logger.LogError(
                        "No se encontró el email del supervisor, no s puede enviar el correo."
                    );
                    return;

                }



                // Reemplazar variables del template.
                var contenidoHtml = BuildTemplate(notificacion, responsable, gasto, sucursal, context);



                // Marcar como PROCESANDO.
                notificacion.IdCatEstatusNotificacion = context.IdEstatusProcesando;

                await db.SaveChangesAsync(stoppingToken);

                _logger.LogInformation(
                    "Procesando notificación {IdNotificacion}.",
                    notificacion.IdNotificacion
                );

                var asunto = notificacion.Intento == 2 ? "Recordatorio: " + context.Template.Asunto : context.Template.Asunto;

                // Intentar enviar correo.
                try
                {
                   
                    await emailSender.Send(
                        responsable.Email,
                        asunto,
                        contenidoHtml
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error al enviar el correo de la notificación {IdNotificacion}.",
                        notificacion.IdNotificacion
                    );

                    // Registrar el intento fallido.
                    notificacion.NumeroReintentos++;

                    // ¿Alcanzó el máximo?
                    if (notificacion.NumeroReintentos >= 10)
                    {
                        notificacion.IdCatEstatusNotificacion =
                            context.IdEstatusError;

                        _logger.LogError(
                            "La notificación {IdNotificacion} alcanzó el máximo de reintentos.",
                            notificacion.IdNotificacion
                        );
                    }
                    else
                    {
                        // Disponible para volver a intentar.
                        notificacion.IdCatEstatusNotificacion =
                            context.IdEstatusPendiente;
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    return;
                }

                // El correo se envió correctamente.
                notificacion.IdCatEstatusNotificacion =
                    context.IdEstatusEnviado;

                notificacion.FechaEnvio = DateTime.Now;

                await db.SaveChangesAsync(stoppingToken);

                _logger.LogInformation(
                    "Correo enviado correctamente para la notificación {IdNotificacion}.",
                    notificacion.IdNotificacion
                );

            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error interno procesando la notificación {IdNotificacion}.",
                    notificacion.IdNotificacion
                );

                notificacion.IdCatEstatusNotificacion =
                    context.IdEstatusError;

                await db.SaveChangesAsync(stoppingToken);
            }
        }

        /// <summary>
        /// Obtiene el registro de estatus de notificación correspondiente al código proporcionado.
        /// </summary>
        /// <param name="db">
        /// Contexto de base de datos utilizado para consultar los estatus de notificación.
        /// </param>
        /// <param name="estatus">
        /// Código o descripción del estatus de notificación que se desea obtener.
        /// </param>
        /// <param name="stoppingToken">
        /// Token utilizado para cancelar la operación asíncrona de consulta.
        /// </param>
        /// <returns>
        /// Registro de <see cref="CatEstatusNotificacion"/> correspondiente al estatus solicitado.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Se produce cuando no existe un estatus de notificación con el valor proporcionado.
        /// </exception>
        private async Task<CatEstatusNotificacion> GetRequiredEstatusNotificacionAsync(
            AppDbContext db,
            string estatus,
            CancellationToken stoppingToken)
        {
            var resultado = await db.CatEstatusNotificacion
                .FirstOrDefaultAsync(
                    x => x.Estatus == estatus,
                    stoppingToken
                );

            if (resultado == null)
            {
                throw new InvalidOperationException(
                    $"No se encontró el estatus de notificación '{estatus}'."
                );
            }

            return resultado;
        }

        /// <summary>
        /// Obtiene el registro de estatus de gasto correspondiente al valor proporcionado.
        /// </summary>
        /// <param name="db">
        /// Contexto de base de datos utilizado para consultar los estatus de gasto.
        /// </param>
        /// <param name="estatus">
        /// Código o descripción del estatus de gasto que se desea obtener.
        /// </param>
        /// <param name="stoppingToken">
        /// Token utilizado para cancelar la operación asíncrona de consulta.
        /// </param>
        /// <returns>
        /// Registro de <see cref="CatEstatusGasto"/> correspondiente al estatus solicitado.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Se produce cuando no existe un estatus de gasto con el valor proporcionado.
        /// </exception>
        private async Task<CatEstatusGasto> GetEstatusGastoAsync(
            AppDbContext db,
            string estatus,
            CancellationToken stoppingToken)
        {
            var resultado = await db.CatEstatusGasto
                .FirstOrDefaultAsync(
                    x => x.Estatus == estatus,
                    stoppingToken
                );

            if (resultado == null)
            {
                throw new InvalidOperationException(
                    $"No se encontró el estatus de notificación '{estatus}'."
                );
            }

            return resultado;
        }

        /// <summary>
        /// Procesa los gastos que requieren una segunda notificación debido a que
        /// excedieron el presupuesto y permanecen sin respuesta después del tiempo
        /// configurado.
        /// </summary>
        /// <param name="stoppingToken">
        /// Token utilizado para cancelar las operaciones asíncronas del proceso.
        /// </param>
        /// <remarks>
        /// El proceso obtiene la configuración necesaria, identifica los gastos cuya
        /// primera notificación fue enviada y que ya superaron el tiempo establecido,
        /// y genera una nueva notificación con <código>Intento = 2</c>.
        /// <para>
        /// Solo se procesan los registros que no cuentan previamente con una segunda
        /// notificación para evitar duplicados.
        /// </para>
        /// <para>
        /// La cantidad de notificaciones generadas por ejecución está limitada por
        /// la configuración <c>CANTIDAD_NOTIFICACIONES_LOTE</c>.
        /// </para>
        /// </remarks>
        private async Task ProcesarSegundasNotificacionesAsync(
                CancellationToken stoppingToken)
        {

            try
            {
                // En cada ejecución del proceso creamos un contexto nuevo.
                using var scope = _scopeFactory.CreateScope();

                // Obtener dependencias necesarias.
                var db = scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

                _logger.LogInformation(
                    "Iniciando procesamiento de notificaciones."
                );


                var estatusEnviado =
                   await GetRequiredEstatusNotificacionAsync(
                       db,
                       Constants.EstatusNotificacionCodigo.ENVIADO,
                       stoppingToken
                   );

                if (estatusEnviado == null)
                {
                    _logger.LogError(
                        "No se encontró el estatus de notificación ENVIADO."
                    );

                    return;
                }

                // Obtener el tipo de notificación.
                var tipoNotificacion = await db.CatTipoNotificacion
                    .FirstOrDefaultAsync(
                        x => x.Evento ==
                            Constants.TipoNotificacionCodigo.PRESUPUESTO_EXCEDIDO,
                        stoppingToken
                    );

                if (tipoNotificacion == null)
                {
                    _logger.LogError(
                        "No se encontró el tipo de notificación PRESUPUESTO_EXCEDIDO."
                    );

                    return;
                }


                var estatusGastoSinRespuesta = await GetEstatusGastoAsync(
                    db, Constants.EstatusGasto.SIN_RESPUESTA, stoppingToken
                    );

                if (estatusGastoSinRespuesta == null)
                {
                    _logger.LogError(
                        "No se encontró el tipo de estatus gasto  SIN_RESPUESTA."
                    );

                    return;
                }

                var configuraciones = await GetNotificationConfigAsync(db, stoppingToken);


                var minutosSegundaNotificacion = GetConfigInt(
                    configuraciones,
                    Constants.ConfiguracionNotificacion
                        .TIEMPO_SEGUNDA_NOTIFICACION_MINUTOS
                );

                var notificacionesPorLote = GetConfigInt(
                    configuraciones,
                    Constants.ConfiguracionNotificacion
                        .CANTIDAD_NOTIFICACIONES_LOTE
                );




                var fechaLimite = DateTime.Now.AddMinutes(-minutosSegundaNotificacion);


                var gastosParaSegundaNotificacion = await (
                    from gasto in db.GastosTarea

                    join tarea in db.Tareas
                        on gasto.IdTarea equals tarea.TareaId

                    join notificacion in db.Notificaciones
                        on gasto.IdGastoTarea equals notificacion.IdReferencia

                    where

                        // El gasto excede presupuesto.
                        gasto.ExecedePresupuesto == true

                        // El gasto continúa SIN_RESPUESTA.
                        && gasto.IdCatEstatusGasto ==
                            estatusGastoSinRespuesta.IdCatEstatusGasto

                        // La notificación corresponde a gastos.
                        && notificacion.TablaReferencia == Constants.TablaReferencia.tb_GastosTarea

                        // La primera notificación fue enviada.
                        && notificacion.IdCatEstatusNotificacion ==
                            estatusEnviado.IdCatEstatusNotificacion

                        // Tipo de notificación.
                        && notificacion.IdTipoNotificacion ==
                            tipoNotificacion.IdTipoNotificacion

                        // Solo el primer intento.
                        && notificacion.Intento == 1

                        // Debe tener fecha de envío.
                        && notificacion.FechaEnvio != null

                        // Ya pasaron 24 horas.
                        && notificacion.FechaEnvio <= fechaLimite

                        // Verificar que NO exista el intento 2 para este gasto.
                        && !db.Notificaciones.Any(notificacion2 =>
                            notificacion2.IdReferencia ==
                                gasto.IdGastoTarea

                            && notificacion2.TablaReferencia ==
                                Constants.TablaReferencia.tb_GastosTarea

                            && notificacion2.IdTipoNotificacion ==
                                tipoNotificacion.IdTipoNotificacion

                            && notificacion2.Intento == 2
                        )

                    orderby notificacion.FechaEnvio

                    select new GastoNotificacionDto
                    {
                        IdGastoTarea = gasto.IdGastoTarea,
                        Gasto = gasto.Gasto,
                        Descripcion = gasto.Descripcion,
                        FechaRegistro = gasto.FechaRegistro,
                        TituloTarea = tarea.Titulo,

                        IdNotificacion = notificacion.IdNotificacion,
                        FechaEnvio = notificacion.FechaEnvio,
                        Intento = notificacion.Intento
                    }
                )
                .AsNoTracking()
                .Take(notificacionesPorLote)
                .ToListAsync(stoppingToken);

                if (gastosParaSegundaNotificacion.Count == 0)
                {
                    _logger.LogInformation(
                        "No se encontraron gastos que requieran una segunda notificación."
                    );

                    return;
                }


                var estatusPendiente =
                    await GetRequiredEstatusNotificacionAsync(
                        db,
                        Constants.EstatusNotificacionCodigo.PENDIENTE,
                        stoppingToken
                    );

                if (estatusPendiente == null)
                {
                    _logger.LogError(
                        "No se encontró el estatus de notificación PENDIENTE."
                    );

                    return;
                }


                var fechaRegistro = DateTime.Now;


                foreach (var item in gastosParaSegundaNotificacion)
                {
                    var notificacion = new Notificaciones
                    {
                        IdTipoNotificacion = tipoNotificacion.IdTipoNotificacion,

                        IdCatEstatusNotificacion = estatusPendiente.IdCatEstatusNotificacion,

                        IdReferencia = item.IdGastoTarea,

                        TablaReferencia = Constants.TablaReferencia.tb_GastosTarea,

                        Intento = 2, // SEGUNDO INTENTO

                        NumeroReintentos = 0,

                        FechaRegistro = fechaRegistro
                    };

                    db.Notificaciones.Add(notificacion);
                }

                //Guardando todas las norticaciones 
                await db.SaveChangesAsync(stoppingToken);


                _logger.LogInformation(
                    "Se guardaron {Cantidad} segundas notificaciones pendientes.",
                    gastosParaSegundaNotificacion.Count
                );


            }
            catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                "Procesamiento de segundas notificaciones cancelado."
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ocurrió un error durante el procesamiento de segundas notificaciones."
                 );
            }


        }


        /// <summary>
        /// Procesa los gastos que requieren una segunda notificación debido a que
        /// excedieron el presupuesto y permanecen sin respuesta después del tiempo
        /// configurado.
        /// </summary>
        /// <param name="stoppingToken">
        /// Token utilizado para cancelar las operaciones asíncronas del proceso.
        /// </param>
        /// <remarks>
        /// El proceso obtiene la configuración necesaria, identifica los gastos cuya
        /// primera notificación fue enviada y que ya superaron el tiempo establecido,
        /// y genera una nueva notificación con <código>Intento = 2</c>.
        /// <para>
        /// Solo se procesan los registros que no cuentan previamente con una segunda
        /// notificación para evitar duplicados.
        /// </para>
        /// <para>
        /// La cantidad de notificaciones generadas por ejecución está limitada por
        /// la configuración <c>CANTIDAD_NOTIFICACIONES_LOTE</c>.
        /// </para>
        /// </remarks>
        private async Task<Dictionary<string, string>>
          GetNotificationConfigAsync(
                AppDbContext db,
                CancellationToken stoppingToken)
        {
            var configuraciones = await db.ConfiguracionNotificacion
                .ToListAsync(stoppingToken);

            if (configuraciones.Count == 0)
            {
                throw new InvalidOperationException(
                    "No se encontraron configuraciones activas."
                );
            }

            return configuraciones.ToDictionary(
                x => x.Clave,
                x => x.Valor
            );
        }

        /// <summary>
        /// Obtiene el valor de una configuración a partir de su clave.
        /// </summary>
        /// <param name="configuraciones">
        /// Diccionario que contiene las configuraciones disponibles,
        /// donde la clave identifica la configuración y el valor contiene su contenido.
        /// </param>
        /// <param name="clave">
        /// Clave de la configuración que se desea obtener.
        /// </param>
        /// <returns>
        /// Valor asociado a la clave proporcionada.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Se produce cuando no existe una configuración asociada a la clave proporcionada.
        /// </exception>
        private string GetConfigString(
        Dictionary<string, string> configuraciones,
        string clave)
        {
            if (!configuraciones.TryGetValue(clave, out var valor))
            {
                throw new InvalidOperationException(
                    $"No se encontró la configuración '{clave}'."
                );
            }

            return valor;
        }


        /// <summary>
        /// Obtiene y convierte el valor de una configuración a un número entero.
        /// </summary>
        /// <param name="configuraciones">
        /// Diccionario que contiene las configuraciones disponibles,
        /// donde la clave identifica la configuración y el valor contiene su contenido.
        /// </param>
        /// <param name="clave">
        /// Clave de la configuración que se desea obtener.
        /// </param>
        /// <returns>
        /// Valor de la configuración convertido a un número entero.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Se produce cuando la configuración no existe o cuando su valor no puede
        /// convertirse a un número entero válido.
        /// </exception>
        private int GetConfigInt(
        Dictionary<string, string> configuraciones,
        string clave)
        {
            var valor = GetConfigString(configuraciones, clave);

            if (!int.TryParse(valor, out var resultado))
            {
                throw new InvalidOperationException(
                    $"La configuración '{clave}' debe ser un número válido. " +
                    $"Valor actual: '{valor}'."
                );
            }

            return resultado;
        }

        /// <summary>
        /// Obtiene la información de los supervisores responsables de las tareas
        /// asociadas a los gastos proporcionados.
        /// </summary>
        /// <param name="nomclickDb">
        /// Contexto de base de datos utilizado para consultar la información
        /// de los empleados y responsables de las acciones.
        /// </param>
        /// <param name="stoppingToken">
        /// Token utilizado para cancelar la operación asíncrona de consulta.
        /// </param>
        /// <param name="gastos">
        /// Lista de gastos cuyas tareas se utilizarán para identificar a los
        /// supervisores correspondientes.
        /// </param>
        /// <returns>
        /// Lista de <see cref="ResponsableNotificacionDto"/> con la información
        /// de los supervisores encontrados para las tareas indicadas.
        /// Si no existen tareas en la lista de gastos, devuelve una lista vacía.
        /// </returns>
        private async Task<List<ResponsableNotificacionDto>> GetSupervisorDataAsync(
        NomclickDbContext nomclickDb,
        CancellationToken stoppingToken,
        List<GastoNotificacionDto> gastos)
        {
            var idsTarea = gastos
                .Select(x => x.IdTarea)
                .Distinct()
                .ToList();


            if (idsTarea.Count == 0)
            {
                return new List<ResponsableNotificacionDto>();
            }

            var parametros = idsTarea
                .Select((id, index) => new SqlParameter($"@id{index}", id))
                .ToArray();

            var parametrosSql = string.Join(", ", parametros.Select(x => x.ParameterName));

            #pragma warning disable EF1002
            var responsables = await nomclickDb.Database
                    .SqlQueryRaw<ResponsableNotificacionDto>(
                                $@"
                        SELECT DISTINCT
                            ac.IdRegistro,
                            empl.IdEmpleado,
                            dbo.decryptValue(empl.Nombres) AS Nombres,
                            dbo.decryptValue(empl.ApellidoPaterno) AS ApellidoPaterno,
                            dbo.decryptValue(empl.ApellidoMaterno) AS ApellidoMaterno,
                            dbo.decryptValue(empl.Email) AS Email
                        FROM tb_Empleados AS empl
                        INNER JOIN tb_Acciones AS ac
                            ON ac.ResponsableSupervicion = empl.IdEmpleado
                        WHERE ac.IdRegistro IN ({parametrosSql})
                    ",
                                parametros)
                    .ToListAsync(stoppingToken);
            #pragma warning restore EF1002

            return responsables;
        }

        /// <summary>
        /// Actualiza la notificación al estatus de error e incrementa el contador
        /// de reintentos realizados.
        /// </summary>
        /// <param name="notificacion">
        /// Notificación que se actualizará.
        /// </param>
        /// <param name="db">
        /// Contexto de base de datos utilizado para guardar los cambios.
        /// </param>
        /// <param name="stoppingToken">
        /// Token utilizado para cancelar la operación asíncrona de persistencia.
        /// </param>
        /// <param name="estatusError">
        /// Estatus de notificación que se asignará como estado de error.
        /// </param>
        /// <returns>
        /// Una tarea que representa la operación asíncrona de actualización.
        /// </returns>
        private async Task SetNotificationEstatusErrorAsync(
         Notificaciones notificacion,
         AppDbContext db,
         CancellationToken stoppingToken,
         CatEstatusNotificacion estatusError)
        {
            notificacion.IdCatEstatusNotificacion =
                estatusError.IdCatEstatusNotificacion;

            notificacion.NumeroReintentos++;

            await db.SaveChangesAsync(stoppingToken);
        }






        /// <summary>
        /// Construye el contenido HTML de la notificación reemplazando las variables
        /// del template con la información correspondiente al responsable, gasto,
        /// sucursal y configuración del proceso.
        /// </summary>
        /// <param name="notificacion">
        /// Notificación que se está procesando. Su número de intento determina si se
        /// utiliza el template original o el template de recordatorio.
        /// </param>
        /// <param name="responsable">
        /// Información del supervisor responsable que recibirá la notificación.
        /// </param>
        /// <param name="gasto">
        /// Información del gasto y de la tarea asociada que será incluida en el template.
        /// </param>
        /// <param name="sucursal">
        /// Información de la sucursal relacionada con el gasto.
        /// </param>
        /// <param name="context">
        /// Configuración del procesamiento de la notificación, incluyendo los templates,
        /// URLs e información necesaria para construir el contenido del correo.
        /// </param>
        /// <returns>
        /// Contenido HTML del template con todas las variables reemplazadas por sus
        /// valores correspondientes.
        /// </returns>
        private string BuildTemplate(
            Notificaciones notificacion,
            ResponsableNotificacionDto responsable,
            GastoNotificacionDto gasto,
            SucursalNotificacionDto sucursal,
            ConfiguracionProcesamientoNotificacion context)
        {
            ValidateTemplateParameters(
                notificacion,
                gasto,
                sucursal,
                context);

            var supervisorName = GetSupervisorName(responsable);
            var sucursalAddress = BuildSucursalAddress(sucursal);
            var urlGasto = BuildGastoUrl(notificacion, context);
            var imagenes = BuildTemplateImagesUrl(context);

            var (Titulo, Monto) = GetPresupuestoInfo(gasto);

            var variables = new Dictionary<string, string>
            {

                ["{NOMBRE_SUPERVISOR}"] = supervisorName ?? string.Empty,
                ["{NOMBRE_TAREA}"] = gasto.TituloTarea ?? string.Empty,
                ["{NOMBRE_SUCURSAL}"] = sucursal.Nombre ?? string.Empty,
                ["{DIRECCION_COMPLETA}"] = sucursalAddress ?? string.Empty,
                ["{DESCRIPCION_TAREA}"] = gasto.DescripcionTarea ?? string.Empty,

                ["{DESCRIPCION_GASTO}"] = gasto.Descripcion ?? string.Empty,
                ["{USUARIO_GASTO}"] = gasto.NombreUsuarioTarea ?? string.Empty,

                ["{FECHA_GASTO}"] = gasto.FechaRegistro.ToString("dd/MM/yyyy"),
                ["{HORA_GASTO}"] = gasto.FechaRegistro.ToString("HH:mm:ss"),

                ["{MONTO_GASTO}"] = FormatCurrency(gasto.Gasto),

                ["{URL_WEB_CLIENTES}"] = urlGasto,

                ["{IMG_LOGO}"] = imagenes.Logo ?? string.Empty,
                ["{IMG_BADGET_PENDING}"] = imagenes.BadgePending ?? string.Empty,
                ["{IMG_CALENDAR_HISTORY}"] = imagenes.HistoryCalendar ?? string.Empty,
                ["{IMG_AVATAR_USER}"] = imagenes.Avatar ?? string.Empty,
                ["{IMG_CLOCK}"] = imagenes.Clock ?? string.Empty,
            };


            string template  =  notificacion.Intento == 2 
                ? context.TemplateReminder.ContenidoHtml 
                : context.Template.ContenidoHtml;

            return ReplaceTemplateVariables(
                template,
                variables);
        }

        /// <summary>
        /// Valida que los parámetros necesarios para construir el template de la
        /// notificación se encuentren disponibles y correctamente configurados.
        /// </summary>
        /// <param name="notificacion">
        /// Notificación que se está procesando.
        /// </param>
        /// <param name="gasto">
        /// Información del gasto asociada a la notificación.
        /// </param>
        /// <param name="sucursal">
        /// Información de la sucursal asociada al gasto.
        /// </param>
        /// <param name="context">
        /// Configuración utilizada para construir el contenido de la notificación,
        /// incluyendo el template y la URL de WebClientes.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Se produce cuando alguno de los parámetros requeridos es nulo.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Se produce cuando el template no está configurado, no contiene contenido HTML
        /// o la URL de WebClientes no está configurada.
        /// </exception>
        private static void ValidateTemplateParameters(
        Notificaciones? notificacion,
        GastoNotificacionDto? gasto,
        SucursalNotificacionDto? sucursal,
        ConfiguracionProcesamientoNotificacion? context)
        {
            ArgumentNullException.ThrowIfNull(notificacion);
            ArgumentNullException.ThrowIfNull(gasto);
            ArgumentNullException.ThrowIfNull(sucursal);
            ArgumentNullException.ThrowIfNull(context);

            if (context.Template == null)
            {
                throw new InvalidOperationException(
                    "No existe una plantilla configurada.");
            }

            if (string.IsNullOrWhiteSpace(context.Template.ContenidoHtml))
            {
                throw new InvalidOperationException(
                    "La plantilla no contiene HTML.");
            }

            if (string.IsNullOrWhiteSpace(context.WebClientesUrl))
            {
                throw new InvalidOperationException(
                    "La URL de WebClientes no está configurada.");
            }
        }


        /// <summary>
        /// Obtiene la información del presupuesto que debe mostrarse en la
        /// notificación, determinando si se debe utilizar el presupuesto final
        /// autorizado o el presupuesto original.
        /// </summary>
        /// <param name="gasto">
        /// Información del gasto que contiene los valores de presupuesto.
        /// </param>
        /// <returns>
        /// Una tupla que contiene el título del presupuesto y el monto correspondiente.
        /// </returns>
        private static (string Titulo, decimal? Monto) GetPresupuestoInfo(
        GastoNotificacionDto gasto)
        {
            return gasto.PresupuestoFinalAutorizado.HasValue
                ? ("Presupuesto final autorizado", gasto.PresupuestoFinalAutorizado.Value)
                : ("Presupuesto original", gasto.PresupuestoOriginal);
        }


        /// <summary>
        /// Convierte un importe a una representación monetaria utilizando formato
        /// de moneda con dos posiciones decimales.
        /// </summary>
        /// <param name="amount">
        /// Importe que se desea formatear.
        /// </param>
        /// <returns>
        /// Importe formateado con símbolo de moneda y separadores de miles.
        /// </returns>
        private static string FormatCurrency(decimal? amount)
        {
            return amount.GetValueOrDefault()
                .ToString("$#,##0.00", CultureInfo.InvariantCulture);
        }


        /// <summary>
        /// Construye la URL que permitirá acceder al gasto específico desde
        /// WebClientes.
        /// </summary>
        /// <param name="notificacion">
        /// Notificación que contiene el identificador del gasto utilizado como
        /// referencia para la redirección.
        /// </param>
        /// <param name="context">
        /// Configuración que contiene la URL base de WebClientes.
        /// </param>
        /// <returns>
        /// URL completa para acceder al gasto asociado a la notificación.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Se produce cuando la URL de WebClientes no está configurada.
        /// </exception>
        private static string BuildGastoUrl(
        Notificaciones notificacion,
        ConfiguracionProcesamientoNotificacion context)
        {
            if (context.WebClientesUrl == null)
            {
                throw new InvalidOperationException(
                    $"No se encontró la url para redirigir"
                );
            }
            var separator = context.WebClientesUrl.Contains('?')
                ? "&"
                : "?";

            return $"{context.WebClientesUrl}/WfPlanDeTrabajo.aspx?accion=5&BG_EX={notificacion.IdReferencia}";
        }

        /// <summary>
        /// Construye las URLs de las imágenes utilizadas dentro del template
        /// de la notificación.
        /// </summary>
        /// <param name="context">
        /// Configuración del procesamiento de la notificación. Debe contener la
        /// URL base de WebClientes utilizada para construir las rutas de las imágenes.
        /// </param>
        /// <returns>
        /// Objeto <see cref="TemplateImagesUrl"/> con las URLs correspondientes
        /// al logotipo, estado pendiente, reloj, calendario y avatar del usuario.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Se produce cuando el contexto proporcionado es nulo.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Se produce cuando la URL base de WebClientes no está configurada.
        /// </exception>
        private static TemplateImagesUrl BuildTemplateImagesUrl(
            ConfiguracionProcesamientoNotificacion context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (string.IsNullOrWhiteSpace(context.WebClientesUrl))
            {
                throw new InvalidOperationException(
                    "No se encontró la URL base de WebClientes para construir las URLs de las imágenes.");
            }

            var urlBase = context.WebClientesUrl.StartsWith("http://local") 
                ?
                "https://veliosclientesdev.adhw.com.mx/admin/AppTema/imagenes/Correos/"
                :
                $"{context.WebClientesUrl.TrimEnd('/')}/AppTema/imagenes/Correos/";


            return new TemplateImagesUrl
            {
                Logo = $"{urlBase}velios-logo.png",
                BadgePending = $"{urlBase}badge-pending.png",
                Clock = $"{urlBase}clock.png",
                HistoryCalendar = $"{urlBase}history-calendar.png",
                Avatar = $"{urlBase}avatar-user.png",
            };

        }

        /// <summary>
        /// Reemplaza las variables definidas dentro de un template por los valores
        /// correspondientes proporcionados en el diccionario.
        /// </summary>
        /// <param name="template">
        /// Contenido del template en el que se realizarán los reemplazos.
        /// </param>
        /// <param name="variables">
        /// Diccionario que contiene las variables a reemplazar como claves y sus
        /// respectivos valores.
        /// </param>
        /// <returns>
        /// Template con todas las variables encontradas reemplazadas por sus valores.
        /// </returns>
        private static string ReplaceTemplateVariables(
        string template,
        Dictionary<string, string> variables)
        {
            foreach (var variable in variables)
            {
                template = template.Replace(
                    variable.Key,
                    variable.Value ?? string.Empty);
            }

            return template;
        }


        /// <summary>
        /// Obtiene el nombre completo del supervisor utilizando sus nombres y
        /// apellido paterno.
        /// </summary>
        /// <param name="responsable">
        /// Información del responsable de la notificación que contiene los datos
        /// del supervisor.
        /// </param>
        /// <returns>
        /// Nombre y apellido paterno del supervisor separados por un espacio.
        /// Si alguno de los valores es nulo, se utiliza una cadena vacía.
        /// </returns>
        private string GetSupervisorName(ResponsableNotificacionDto responsable)
        {
            string nombres = responsable.Nombres ?? "";
            string apellidoPaterno = responsable.ApellidoPaterno ?? "";

            return string.Concat(nombres, " ", apellidoPaterno);


        }



        /// <summary>
        /// Obtiene la información de las sucursales asociadas a las tareas de los
        /// gastos proporcionados.
        /// </summary>
        /// <param name="nomclickDb">
        /// Contexto de base de datos utilizado para consultar la información de las sucursales.
        /// </param>
        /// <param name="stoppingToken">
        /// Token utilizado para cancelar la operación asíncrona de consulta.
        /// </param>
        /// <param name="gastos">
        /// Lista de gastos cuyas tareas se utilizarán para identificar las sucursales
        /// correspondientes.
        /// </param>
        /// <returns>
        /// Lista de <see cref="SucursalNotificacionDto"/> con la información de las
        /// sucursales asociadas a las tareas.
        /// Si no existen tareas en la lista de gastos, se devuelve una lista vacía.
        /// </returns>
        /// <remarks>
        /// La consulta obtiene el nombre y domicilio de cada sucursal a partir del
        /// centro de trabajo asociado a la tarea. Los valores de colonia, municipio,
        /// ciudad y estado se establecen como <see langword="null"/> cuando su
        /// descripción corresponde a un valor genérico como "sin colonia",
        /// "sin delegación", "sin ciudad" o "sin estado".
        /// </remarks>
        private async Task<List<SucursalNotificacionDto>> GetSucursalDataAsync(
            NomclickDbContext nomclickDb,
            CancellationToken stoppingToken,
            List<GastoNotificacionDto> gastos)
        {
            var idsTarea = gastos
                .Select(x => x.IdTarea)
                .Distinct()
                .ToList();

            if (idsTarea.Count == 0)
            {
                return [];
            }

            var parametros = idsTarea
                .Select((id, index) => new SqlParameter($"@id{index}", id))
                .ToArray();

            var idsParametros = string.Join(
                ", ",
                parametros.Select(p => p.ParameterName)
            );

            #pragma warning disable EF1002

            var sucursalData = await nomclickDb.Database
                .SqlQueryRaw<SucursalNotificacionDto>(
                    $@"
                SELECT 
                    ac.idRegistro AS TareaId,

                    ct.nombre AS Nombre,
                    ct.calle AS Calle,
                    ct.numeroExterior AS NumeroExterior,

                    CASE
                        WHEN LOWER(LTRIM(RTRIM(cp.DescColonia))) = 'sin colonia'
                            THEN NULL
                        ELSE cp.DescColonia
                    END AS Colonia,

                    CASE
                        WHEN LOWER(LTRIM(RTRIM(cp.DescDelegMunic))) = 'sin delegación'
                            THEN NULL
                        ELSE cp.DescDelegMunic
                    END AS Municipio,

                    CASE
                        WHEN LOWER(LTRIM(RTRIM(cp.DescCiudad))) = 'sin ciudad'
                            THEN NULL
                        ELSE cp.DescCiudad
                    END AS Ciudad,

                    CASE
                        WHEN LOWER(LTRIM(RTRIM(cp.DescEstado))) = 'sin estado'
                            THEN NULL
                        ELSE cp.DescEstado
                    END AS Estado,

                    cp.DescPais AS Pais,
                    cp.CodigoPostal AS CodigoPostal

                FROM tb_CentroDeTrabajo ct

                INNER JOIN tb_PlanDeTrabajo pt 
                    ON pt.idCentroDeTrabajo = ct.idCentroDeTrabajo

                INNER JOIN tb_Acciones ac
                    ON ac.idPlanDeTrabajo = pt.idPlanDeTrabajo

                LEFT JOIN tb_CodigosPostales cp
                    ON cp.idCP = ct.idCP

                WHERE ac.idRegistro IN ({idsParametros})
            ",
                    parametros)
                .ToListAsync(stoppingToken);

            #pragma warning restore EF1002

            return sucursalData;
        }


        /// <summary>
        /// Construye la dirección completa de una sucursal a partir de sus datos
        /// de ubicación, omitiendo los valores que no estén disponibles.
        /// </summary>
        /// <param name="sucursal">
        /// Información de la sucursal que contiene los datos de su domicilio.
        /// </param>
        /// <returns>
        /// Dirección completa de la sucursal, con sus componentes separados por comas.
        /// </returns>
        private string BuildSucursalAddress(
        SucursalNotificacionDto sucursal)
        {
            var partes = new List<string>();

            if (!string.IsNullOrWhiteSpace(sucursal.Calle))
                partes.Add(sucursal.Calle);

            if (!string.IsNullOrWhiteSpace(sucursal.NumeroExterior))
                partes.Add($"No. {sucursal.NumeroExterior}");

            if (!string.IsNullOrWhiteSpace(sucursal.Colonia))
                partes.Add(sucursal.Colonia);

            if (!string.IsNullOrWhiteSpace(sucursal.Municipio))
                partes.Add(sucursal.Municipio);

            if (!string.IsNullOrWhiteSpace(sucursal.Ciudad))
                partes.Add(sucursal.Ciudad);

            if (!string.IsNullOrWhiteSpace(sucursal.Estado))
                partes.Add(sucursal.Estado);

            if (!string.IsNullOrWhiteSpace(sucursal.Pais))
                partes.Add(sucursal.Pais);

            if (!string.IsNullOrWhiteSpace(sucursal.CodigoPostal))
                partes.Add($"C.P. {sucursal.CodigoPostal}");

            return string.Join(", ", partes);
        }

    }


}
