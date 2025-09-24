using Core.Interface;
using Microsoft.AspNetCore.Mvc;
using Scale.io_API.Controllers.Abstract;

namespace Scale.io_API.Controllers.Foods;

public class FreshFoodsController(IFreshFoodsService service) : Controller<IFreshFoodsService>(service)
{
    [HttpGet("{food}")]
    public Task<ActionResult> Read(string food, [FromQuery] double grams)
    {
        return base.Read(food, grams);
    }
}