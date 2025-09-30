using Core.Interface.Login;
using Core.Models.API.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers.Auth;

[ApiController]
[Route("[controller]")]
public class AuthController(IAuthService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult> Authenticate(LoginRequest request)
    {
        var res = await service.Authenticate(request);
        return res == null ? Unauthorized() : Ok(res);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var res = await service.Refresh(request);
        return res == null ? BadRequest("The given token is not valid.") : Ok(res);
    }
}