namespace velios.Api.Models.Notificaciones
{
    public class ResponsableNotificacionDto
    {

        // Este valor corresponde a IdTarea en la otra base.
        public int IdRegistro { get; set; }

        public int IdEmpleado { get; set; }

        public string? Nombres { get; set; }

        public string? ApellidoPaterno { get; set; }

        public string? ApellidoMaterno { get; set; }

        public string? Email { get; set; }
    }
}
