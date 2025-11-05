using Core.Interface;
using Core.Models.API.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers.Meals;

[ApiController]
[Authorize]
[Route("[controller]")]
public class MealsController : ControllerBase
{
    [HttpGet]
    public async Task<OkObjectResult> Create(MealCreationRequest request)
    {
        var user = User.Identity?.Name;

        return await Task.FromResult(
            Ok(new { user })
        );
    }
}