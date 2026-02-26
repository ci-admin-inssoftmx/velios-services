namespace velios.Api.Models.Common;

public class ApiResponse<T>
{
    public string request_id { get; set; } = Guid.NewGuid().ToString();
    public bool success { get; set; }
    public string message { get; set; } = "";
    public T? data { get; set; }
    public int statusCode { get; set; }
    public string? field { get; set; }
    public string? code { get; set; }
    public List<string>? errors { get; set; }
}