using Core.DTO.Foods;
using Core.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers.Abstract;

[ApiController]
[Route("[controller]")]
public abstract class Controller<T>(T service) : ControllerBase where T : IService
{
    protected virtual bool EmptyAsNotFound => false;

    protected async Task<ActionResult> Read(string input, double? grams)
    {
        var response = await service.FetchAsync(input, grams);
        if (response is null) return NotFound();

        return IsEmpty(response) && EmptyAsNotFound ? NotFound() : Ok(response);
    }

    private static bool IsEmpty(FoodResponse response)
    {
        return response.Foods.Length == 0;
    }
}