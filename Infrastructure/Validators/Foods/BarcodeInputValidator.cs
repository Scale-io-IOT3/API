

namespace Infrastructure.Validators.Foods;

/// <summary>Normalizes barcode input and applies the lookup boundary's length and check-digit rules.</summary>
internal static class BarcodeInputValidator
{
    private const int MinBarcodeLength = 8;

    private const int MaxBarcodeLength = 14;

    /// <summary>Extracts digits and validates an 8-to-14-digit code using its modulo-10 check digit.</summary>
    /// <param name="input">The raw barcode input; whitespace or empty input is rejected.</param>
    /// <param name="barcode">The extracted digits, including when validation fails.</param>
    /// <returns>True when the extracted code passes both rules.</returns>
    /// <remarks>The length rule accepts the entire range, not only standard GTIN lengths.</remarks>
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
