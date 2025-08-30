using Core.DTO.FreshFoods;
using Core.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers;

[ApiController]
[Route("v{v:apiVersion}/[controller]")]
public class FoodsController(IService<FoodResponse> service) : ControllerBase
{
    [HttpGet("{food}")]
    public async Task<ActionResult> Read(string food)
    {
        var res = await service.FetchAsync(food);
        return res is not null ? Ok(res) : NotFound();
    }
}