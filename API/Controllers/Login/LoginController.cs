using Core.Interface.Login;
using Core.Models.API.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers.Login;

[ApiController]
[Route("[controller]")]
public class LoginController(ILoginService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult> Authenticate(LoginRequest request)
    {
        var res = await service.Authenticate(request);
        return res == null ? Unauthorized() : Ok(res);
    }
}