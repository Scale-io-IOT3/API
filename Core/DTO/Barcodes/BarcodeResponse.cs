using System.Text.Json.Serialization;
using Core.Interface;

namespace Core.DTO.Barcodes;

public class BarcodeResponse : IResponse
{
    [JsonPropertyName("product")] public Product Product { get; init; }
}