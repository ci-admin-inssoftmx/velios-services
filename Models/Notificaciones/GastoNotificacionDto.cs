namespace velios.Api.Models.Notificaciones
{
    public class GastoNotificacionDto
    {
        public int IdGastoTarea { get; set; }

        public int IdTarea { get; set; }

        public decimal Gasto { get; set; }

        public string? Descripcion { get; set; }

        public DateTime FechaRegistro { get; set; }

        public string TituloTarea { get; set; } = string.Empty;

        public long IdNotificacion { get; set; }

        public DateTime? FechaEnvio { get; set; }


        public int Intento { get; set; }

        public string? DescripcionTarea { get; set; } = string.Empty;
        public decimal? PresupuestoOriginal { get; set; }
        public decimal? PresupuestoDisponible { get; set; }

        public decimal? PresupuestoUsado { get; set; }

        public decimal? PresupuestoFinalAutorizado { get; set; }

        public int? RegisteredById { get; set; }

        public string? RegisteredByType { get; set; }
        public string? NombreUsuarioTarea { get; set; }


        













    }
}
