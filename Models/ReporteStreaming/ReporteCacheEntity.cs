using System;

namespace velios.Api.Models
{
    /// <summary>
    /// Representa un registro de tb_ReporteCache: el estado de un reporte
    /// solicitado, ya sea en generación o ya completado y listo para descarga directa.
    /// </summary>
    public class ReporteCacheEntity
    {
        public long Id { get; set; }
        public string ClaveHash { get; set; }
        public string TipoReporte { get; set; }
        public string FiltrosJson { get; set; }
        public int? ProveedorId { get; set; }
        public int? ClienteId { get; set; }
        public int UsuarioSolicitanteId { get; set; }

        public string Estado { get; set; } // Procesando | Completado | Error | Expirado

        public string RutaAlmacenamiento { get; set; }
        public string UrlDescarga { get; set; }

        public int? TotalRegistros { get; set; }
        public int? RegistrosProcesados { get; set; }

        public string HangfireJobId { get; set; }

        public DateTime FechaSolicitud { get; set; }
        public DateTime? FechaCompletado { get; set; }
        public DateTime? FechaExpiracion { get; set; }

        public string MensajeError { get; set; }
    }

    public static class ReporteCacheEstados
    {
        public const string Procesando = "Procesando";
        public const string Completado = "Completado";
        public const string Error = "Error";
        public const string Expirado = "Expirado";
    }
}