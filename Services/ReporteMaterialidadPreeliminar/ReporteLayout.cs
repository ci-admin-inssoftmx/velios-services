using System.Text.RegularExpressions;

namespace velios.Api.Services;

/// <summary>
/// Utilidades para que los textos variables no descuadren los PDF de materialidad.
/// </summary>
internal static class ReporteLayout
{
    /// <summary>Corta en una palabra completa y agrega "..." si excede el máximo.</summary>
    public static string Truncar(string? texto, int max)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var limpio = Regex.Replace(texto.Trim(), @"\s+", " ");
        if (limpio.Length <= max) return limpio;

        var corte = limpio.Substring(0, max);
        var ultimoEspacio = corte.LastIndexOf(' ');
        if (ultimoEspacio > max * 0.6) corte = corte.Substring(0, ultimoEspacio);

        return corte.TrimEnd(' ', ',', '.', ';', '-', '/') + "...";
    }

    /// <summary>
    /// Reduce el tamaño de letra de forma suave cuando el texto pasa de "umbral" caracteres,
    /// sin bajar de minSize.
    /// </summary>
    public static float Tamano(string? texto, float baseSize, float minSize, int umbral)
    {
        var len = string.IsNullOrWhiteSpace(texto) ? 0 : texto.Trim().Length;
        if (len <= umbral) return baseSize;

        var size = baseSize * MathF.Sqrt((float)umbral / len);
        return MathF.Round(MathF.Max(minSize, size) * 2f) / 2f;
    }

    /// <summary>Trunca y calcula el tamaño de letra en un solo paso.</summary>
    public static (string Texto, float Size) Ajustar(
        string? texto, int maxCaracteres, float baseSize, float minSize, int umbral)
    {
        var t = Truncar(texto, maxCaracteres);
        return (t, Tamano(t, baseSize, minSize, umbral));
    }
}