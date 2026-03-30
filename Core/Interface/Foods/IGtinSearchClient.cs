using Core.DTO.GtinSearch;

namespace Core.Interface.Foods;

public interface IGtinSearchClient
{
    Task<GtinSearchItem[]> LookupBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<GtinSearchItem[]> SearchAsync(string query, CancellationToken cancellationToken = default);
}
