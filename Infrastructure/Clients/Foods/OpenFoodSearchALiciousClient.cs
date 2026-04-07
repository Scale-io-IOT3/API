using Core.DTO.OpenFoodFacts;
using Infrastructure.Clients.Abstract;

namespace Infrastructure.Clients.Foods;

public class OpenFoodSearchALiciousClient(HttpClient client) : Client<OpenFoodSearchALiciousResponse>(client)
{
    private const string Url = "https://search.openfoodfacts.org/search";

    protected override IEnumerable<string> Requests(string url)
    {
        var query = base.Request(url);
        var fields = "product_name,product_name_en,product_name_fr,brands,nutriscore_grade,nutrition_grades,nutrition_grade_fr,nutrition_grades_tags,nutrient_levels";

        yield return $"{Url}?q={query}&page_size=25&fields={fields}";
    }
}
