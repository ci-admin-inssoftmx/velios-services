using System.Text.Json;

public interface IGeocodingService
{
    Task<(decimal lat, decimal lng)?> GeocodificarAsync(string direccion);
}

public class GeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private const string ApiKey = "AIzaSyDhE72qyJLheqGX3FaS9_JSiFQkTAMU-rg"; // misma key que ya usas en Static Maps / geocoding inverso del frontend

    public GeocodingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(decimal lat, decimal lng)?> GeocodificarAsync(string direccion)
    {
        if (string.IsNullOrWhiteSpace(direccion))
            return null;

        var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(direccion)}&key={ApiKey}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var status = doc.RootElement.GetProperty("status").GetString();
            if (status != "OK") return null;

            var results = doc.RootElement.GetProperty("results");
            if (results.GetArrayLength() == 0) return null;

            var location = results[0].GetProperty("geometry").GetProperty("location");
            var lat = location.GetProperty("lat").GetDecimal();
            var lng = location.GetProperty("lng").GetDecimal();

            return (lat, lng);
        }
        catch
        {
            // Si Google falla o no hay conexión, no bloqueamos el guardado —
            // la parada se guarda solo con la dirección de texto, sin coordenadas.
            return null;
        }
    }
}