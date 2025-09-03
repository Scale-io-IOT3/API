using Core.Interface;
using Microsoft.AspNetCore.Mvc;
using Scale.io_API.Controllers.Abstract;

namespace Scale.io_API.Controllers;

public class FoodsController(IFreshFoodsService service) : Controller<IFreshFoodsService>(service)
{
    [HttpGet("{food}")]
    public Task<ActionResult> Read(string food, [FromQuery] double grams) => base.Read(food, grams);
}