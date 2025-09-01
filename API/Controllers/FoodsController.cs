using Core.Interface;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Scale.io_API.Controllers.Abstract;

namespace Scale.io_API.Controllers;

[ApiController]
[Route("v{v:apiVersion}/[controller]")]
public class FoodsController(ServiceFactory factory) : Controller<IFreshFoodsService>(factory)
{
    [HttpGet("{food}")]
    public async Task<ActionResult> Read(string food, [FromQuery] double grams) => await base.Read(food, grams);
}