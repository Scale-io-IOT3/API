

namespace Infrastructure.Validators.Foods;

internal static class BarcodeInputValidator
{
    private const int MinBarcodeLength = 8;

    private const int MaxBarcodeLength = 14;

    internal static bool TryNormalize(string input, out string barcode)
    {
        barcode = NormalizeBarcode(input);
        return IsValidBarcode(barcode) && HasValidCheckDigit(barcode);
    }

    private static bool IsValidBarcode(string barcode)
    {
        return barcode.Length is >= MinBarcodeLength and <= MaxBarcodeLength;
    }

    private static bool HasValidCheckDigit(string code)
    {
        if (code.Length < 2 || code.Any(c => !char.IsDigit(c)))
        {
            return false;
        }

        var checkDigit = code[^1] - '0';
        var sum = 0;
        var positionFromRight = 1;

        for (var i = code.Length - 2; i >= 0; i--)
        {
            var digit = code[i] - '0';
            var weight = positionFromRight % 2 == 1 ? 3 : 1;
            sum += digit * weight;
            positionFromRight++;
        }

        var computed = (10 - (sum % 10)) % 10;
        return computed == checkDigit;
    }

    private static string NormalizeBarcode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = value.Where(char.IsDigit).ToArray();
        return new string(digits);
    }
}
