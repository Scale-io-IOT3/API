using Core.Interface;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Scale.io_API.Controllers.Abstract;

namespace Scale.io_API.Controllers;

[ApiController]
[Route("v{v:apiVersion}/[controller]")]
public class BarcodesController(ServiceFactory factory) : Controller<IBarcodeService>(factory)
{
    [HttpGet("{code}")]
    public Task<ActionResult> Read(string code, [FromQuery] double grams) => base.Read(code, grams);
}