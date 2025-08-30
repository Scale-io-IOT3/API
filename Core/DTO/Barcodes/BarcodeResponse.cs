using System.Text.Json.Serialization;

namespace Core.DTO;

public class BarcodeResponse
{
    [JsonPropertyName("code")]
    public string Code { get; init; }
    
    [JsonPropertyName("product")]
    public Product Product { get; init; }
}