using Core.Interface.Meals;
using Core.Models.API.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers.Meals;

[ApiController]
[Authorize]
[Route("[controller]")]
public class MealsController(IMealsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Create(MealCreationRequest request)
    {
        var username = User.Identity?.Name;
        if (username is null) return Unauthorized();

        var res = await service.RegisterAsync(request, username);

        return Ok(res);
    }
}