using Core.DTO.OpenFoodFacts;
using Infrastructure.Clients.Abstract;

namespace Infrastructure.Clients.Foods;

public class OpenFoodSearchClient(HttpClient client) : Client<OpenFoodSearchResponse>(client)
{
    private const string PrimaryUrl = "https://world.openfoodfacts.net/api/v2/search";
    private const string FallbackUrl = "https://world.openfoodfacts.org/api/v2/search";

    protected override IEnumerable<string> Requests(string url)
    {
        var query = base.Request(url);
        var fields = "product_name,brands,nutriscore_grade,nutrition_grades,nutrition_grade_fr,nutrition_grades_tags,nutriments";

        yield return $"{PrimaryUrl}?search_terms={query}&page_size=25&fields={fields}";
        yield return $"{FallbackUrl}?search_terms={query}&page_size=25&fields={fields}";
    }
}
