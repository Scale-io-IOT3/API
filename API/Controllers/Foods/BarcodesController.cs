using Core.Interface.Foods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scale.io_API.Controllers.Foods.Abstract;

namespace Scale.io_API.Controllers.Foods;

[Authorize]
public class BarcodesController(IBarcodeService service, ILogger<BarcodesController> logger)
    : FoodsController<IBarcodeService>(service, logger)
{
    protected override bool EmptyAsNotFound => true;

    [HttpGet("{code}")]
    public Task<ActionResult> Read(string code, [FromQuery] double grams)
    {
        return base.Read(code, grams);
    }
}