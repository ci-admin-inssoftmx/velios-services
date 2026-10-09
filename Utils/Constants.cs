namespace velios.Api.Utils
{
    public class Constants
    {


        public static class TipoNotificacionCodigo
        {
            public const string PRESUPUESTO_EXCEDIDO = "PRESUPUESTO_EXCEDIDO";
            public const string RECORDATORIO_PRESUPUESTO_EXCEDIDO = "RECORDATORIO_PRESUPUESTO_EXCEDIDO";
        }

        public static class EstatusNotificacionCodigo
        {
            public const string PENDIENTE = "PENDIENTE";
            public const string PROCESANDO = "PROCESANDO";
            public const string ENVIADO = "ENVIADO";
            public const string ERROR = "ERROR";
            public const string NO_NECESARIO = "NO_NECESARIO";
        }


        public static class EstatusGasto
        {
            public const string APROBADO = "APROBADO";
            public const string RECHAZADO = "RECHAZADO";
            public const string SIN_RESPUESTA = "SIN_RESPUESTA";
        }


        public static class TablaReferencia
        {
            public const string tb_GastosTarea = "tb_GastosTarea";
        }

        public static class ConfiguracionNotificacion
        {
            public const string TIEMPO_SEGUNDA_NOTIFICACION_MINUTOS = "TIEMPO_SEGUNDA_NOTIFICACION_MINUTOS";
            public const string MAXIMO_REINTENTOS = "MAXIMO_REINTENTOS";
            public const string CANTIDAD_NOTIFICACIONES_LOTE = "CANTIDAD_NOTIFICACIONES_LOTE";
            public const string WEB_CLIENTE_URL = "WEB_CLIENTE_URL";

        }

    }

}
