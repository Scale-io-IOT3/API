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
    [HttpPost]
    public async Task<ActionResult> Create(MealCreationRequest request)
    {
        var username = User.Identity?.Name;
        if (username is null) return Unauthorized();

        var res = await service.RegisterAsync(request, username);
        return res is not null ? Ok(res) : BadRequest();
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var username = User.Identity?.Name;
        if (username is null) return Unauthorized();

        var res = (await service.GetMeals(username)).Select(m => m.ToDto()).ToList();
        return Ok(res);
    }
}