using Core.DTO;
using Core.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers;

[ApiController]
[Route("v{v:apiVersion}/[controller]")]
public class BarcodesController(IService<BarcodeResponse> service) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<ActionResult> Read(string code, [FromQuery] double grams)
    {
        var res = await service.FetchAsync(code, grams);
        return res is not null ? Ok(res) : NotFound();
    }
}