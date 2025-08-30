using Core.DTO;

namespace Core.Interface;

public interface IBarcodeService
{
    public Task<BarcodeResponse?> FetchProduct(string code, double grams);
}