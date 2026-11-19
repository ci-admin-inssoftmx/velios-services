using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace velios.Api.Services
{
    /// <summary>
    /// Genera la clave (hash) que identifica de forma única una combinación de
    /// tipo de reporte + filtros + ámbito (proveedor/cliente). Dos solicitudes con
    /// exactamente los mismos parámetros producen la misma clave, lo que permite
    /// reutilizar un reporte ya generado.
    /// </summary>
    public static class ReporteCacheKeyBuilder
    {
        private static readonly JsonSerializerOptions Opciones = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = null
        };

        public static string Calcular(string tipoReporte, object filtros, int? proveedorId, int? clienteId)
        {
            if (string.IsNullOrWhiteSpace(tipoReporte))
                throw new ArgumentException("tipoReporte es requerido", nameof(tipoReporte));

            var filtrosJson = JsonSerializer.Serialize(filtros ?? new object(), Opciones);
            var payload = $"{tipoReporte}|{proveedorId}|{clienteId}|{filtrosJson}";

            var bytes = Encoding.UTF8.GetBytes(payload);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash); // 64 caracteres hex, calza con CHAR(64) en la tabla
        }
    }
}