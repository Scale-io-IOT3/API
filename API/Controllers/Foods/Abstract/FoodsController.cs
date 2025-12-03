using Core.DTO.Foods;
using Core.Interface.Foods;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers.Foods.Abstract;

[ApiController]
[Route("[controller]")]
public abstract class FoodsController<T>(T service, ILogger<FoodsController<T>> logger)
    : ControllerBase where T : IFoodService
{
    protected virtual bool EmptyAsNotFound => false;

    protected async Task<ActionResult> Read(string query, double? grams)
    {
        LogRequestStart(query, grams);

        var response = await service.FetchAsync(query, grams);
        if (response is null) return LogAndReturnNotFound("Service returned null response", query, grams);

        if (IsEmpty(response))
            return EmptyAsNotFound
                ? LogAndReturnNotFound("No foods found", query, grams)
                : LogAndReturnOk("Empty foods list returned", query, grams, response);


        return LogAndReturnOk("Successfully fetched foods", query, grams, response);
    }

    private void LogRequestStart(string query, double? grams)
    {
        logger.LogInformation("➡️ Requested {Grams}g of '{Query}' at {Controller}",
            grams,
            query,
            typeof(T).Name
        );
    }

    private OkObjectResult LogAndReturnOk(string message, string query, double? grams, FoodResponse response)
    {
        logger.LogInformation("{Message}. Count={Count}, query='{Query}', grams={Grams}",
            message,
            response.Foods.Length,
            query,
            grams
        );

        return Ok(response);
    }

    private NotFoundResult LogAndReturnNotFound(string message, string query, double? grams)
    {
        logger.LogWarning("{Message}. query='{Query}', grams={Grams}", message, query, grams);
        return NotFound();
    }

    private static bool IsEmpty(FoodResponse response)
    {
        return response.Foods.Length == 0;
    }
}