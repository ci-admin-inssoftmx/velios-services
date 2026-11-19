using System;

namespace velios.Api.Models
{
    /// <summary>
    /// Estado de progreso de un job de generación de reporte, identificado por JobId.
    /// </summary>
    public class ProgresoReporte
    {
        public string JobId { get; set; }
        public string Estado { get; set; } = "Procesando"; // Procesando | Completado | Error

        public int TotalRegistros { get; set; }
        public int RegistrosProcesados { get; set; }

        public string UrlDescarga { get; set; }
        public string MensajeError { get; set; }

        public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
        public DateTime? FechaActualizacion { get; set; }

        public int PorcentajeCompletado =>
            TotalRegistros <= 0 ? 0 : (int)Math.Round(RegistrosProcesados * 100.0 / TotalRegistros);
    }
}