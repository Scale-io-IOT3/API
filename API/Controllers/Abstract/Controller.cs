using Core.Interface;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers.Abstract;

public abstract class Controller<TService>(TService service) : ControllerBase where TService : IService
{
    protected async Task<ActionResult> Read(string input, double? grams)
    {
        var res = await service.FetchAsync(input, grams);
        return res is not null ? Ok(res) : NotFound();
    }
}