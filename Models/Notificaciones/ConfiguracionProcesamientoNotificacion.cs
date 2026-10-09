namespace velios.Api.Models.Notificaciones
{
    public class ConfiguracionProcesamientoNotificacion
    {
        public  int IdEstatusPendiente { get; init; }
        public  int IdEstatusProcesando { get; init; }
        public  int IdEstatusEnviado { get; init; }
        public  int IdEstatusError { get; init; }
        public  int IdTipoNotificacion { get; init; }
        public  int IdEstatusNotificacion { get; init; }

        public  string? WebClientesUrl { get; init; }

        public required CatTemplateNotificacion Template { get; init; }
        public required CatTemplateNotificacion TemplateReminder { get; init; }
    }
}
