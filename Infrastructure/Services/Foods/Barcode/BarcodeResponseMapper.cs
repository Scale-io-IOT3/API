using Core.DTO.Foods;
using static Infrastructure.Services.Foods.Barcode.BarcodeModels;

namespace Infrastructure.Services.Foods.Barcode;

internal static class BarcodeResponseMapper
{
    internal static FoodDto ToDto(ConsensusFood consensus, double grams)
    {
        var dto = new FoodDto
        {
            HiddenName = consensus.Name,
            Brands = consensus.Brand,
            HiddenMacrosDto = MacrosDto.From(
                consensus.Carbohydrates,
                consensus.Fat,
                consensus.Proteins,
                (int)Math.Round(consensus.Calories)
            ),
            Grade = consensus.Grade,
            Confidence = Math.Round(consensus.Confidence, 3),
            SourcesUsed = consensus.Sources
        };

        dto.Scale(grams);
        return dto;
    }
}
