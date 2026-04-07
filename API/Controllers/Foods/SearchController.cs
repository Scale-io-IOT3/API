using Core.Interface.Foods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scale.io_API.Controllers.Foods.Abstract;

namespace Scale.io_API.Controllers.Foods;

[Authorize]
public class SearchController(IFreshFoodsService service, ILogger<SearchController> logger)
    : FoodsController<IFreshFoodsService>(service, logger)
{
    [HttpGet("{food}")]
    public Task<ActionResult> Read(string food, [FromQuery] double grams)
    {
        return base.Read(food, grams);
    }
}