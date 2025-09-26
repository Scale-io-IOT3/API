using System.Text.Json.Serialization;
using Core.Interface;

namespace Core.DTO.Barcodes;

public class BarcodeResponse : IResponse
{
    [JsonPropertyName("product")] public required Product Product { get; init; }
}