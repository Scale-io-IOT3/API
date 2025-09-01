using System.Text.Json.Serialization;
using Core.Interface;

namespace Core.DTO.Barcodes;

public class BarcodeResponse : ISourceResponse
{
    [JsonPropertyName("product")] public Product Product { get; init; }
}