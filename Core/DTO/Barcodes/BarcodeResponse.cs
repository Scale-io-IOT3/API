using System.Text.Json.Serialization;
using Core.DTO.Barcodes;

namespace Core.DTO;

public class BarcodeResponse
{
    [JsonPropertyName("product")] public Product Product { get; init; }
}