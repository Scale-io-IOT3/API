using Core.Interface;
using Microsoft.AspNetCore.Mvc;
using Scale.io_API.Controllers.Abstract;

namespace Scale.io_API.Controllers;

public class BarcodesController(IBarcodeService service) : Controller<IBarcodeService>(service)
{
    protected override bool EmptyAsNotFound => true;

    public Task<ActionResult> Read(string code, [FromQuery] double grams)
    {
        return base.Read(code, grams);
    }
}