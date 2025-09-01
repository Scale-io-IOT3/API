using Core.Interface;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers.Abstract;

public abstract class Controller<T>(ServiceFactory factory) : ControllerBase where T : IService
{
    private readonly T _service = factory.GetService<T>();

    protected async Task<ActionResult> Read(string input, double? @params)
    {
        var res = await _service.FetchAsync(input, @params);
        return res is not null ? Ok(res) : NotFound();
    }
}
