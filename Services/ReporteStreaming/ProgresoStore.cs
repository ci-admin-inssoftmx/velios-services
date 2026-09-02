using System;
using System.Collections.Concurrent;
using velios.Api.Models;

namespace velios.Api.Services
{
    public interface IProgresoStore
    {
        void Iniciar(string jobId);
        void ActualizarTotal(string jobId, int total);
        void ActualizarProgreso(string jobId, int registrosProcesados);
        void MarcarCompletado(string jobId, string urlDescarga);
        void MarcarError(string jobId, string mensajeError);
        ProgresoReporte Obtener(string jobId);
    }

    /// <summary>
    /// Almacén en memoria del progreso de generación de reportes, indexado por JobId.
    /// Si el servicio corre en más de una instancia (varios nodos IIS balanceados),
    /// esto debe migrarse a un store distribuido (Redis) en vez de memoria local,
    /// porque el polling podría caer en una instancia distinta a la que corrió el job.
    /// </summary>
    public class ProgresoStore : IProgresoStore
    {
        private readonly ConcurrentDictionary<string, ProgresoReporte> _progresos = new();

        public void Iniciar(string jobId)
        {
            _progresos[jobId] = new ProgresoReporte
            {
                JobId = jobId,
                Estado = "Procesando",
                FechaInicio = DateTime.UtcNow
            };
        }

        public void ActualizarTotal(string jobId, int total)
        {
            if (_progresos.TryGetValue(jobId, out var p))
            {
                p.TotalRegistros = total;
                p.FechaActualizacion = DateTime.UtcNow;
            }
        }

        public void ActualizarProgreso(string jobId, int registrosProcesados)
        {
            if (_progresos.TryGetValue(jobId, out var p))
            {
                p.RegistrosProcesados = registrosProcesados;
                p.FechaActualizacion = DateTime.UtcNow;
            }
        }

        public void MarcarCompletado(string jobId, string urlDescarga)
        {
            if (_progresos.TryGetValue(jobId, out var p))
            {
                p.Estado = "Completado";
                p.UrlDescarga = urlDescarga;
                p.RegistrosProcesados = p.TotalRegistros;
                p.FechaActualizacion = DateTime.UtcNow;
            }
        }

        public void MarcarError(string jobId, string mensajeError)
        {
            if (_progresos.TryGetValue(jobId, out var p))
            {
                p.Estado = "Error";
                p.MensajeError = mensajeError;
                p.FechaActualizacion = DateTime.UtcNow;
            }
        }

        public ProgresoReporte Obtener(string jobId)
        {
            return _progresos.TryGetValue(jobId, out var p) ? p : null;
        }
    }
}