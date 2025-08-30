using Core.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers;

[ApiController]
[Route("v{v:apiVersion}/[controller]")]
public class BarcodesController(IBarcodeService service) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<ActionResult> Read(string code, [FromQuery] double grams)
    {
        var res = await service.FetchProduct(code, grams);
        return res is not null ? Ok(res) : NotFound();
    }
}