using Core.Interface.Login;
using Core.Models.API.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Scale.io_API.Controllers.Auth;

[ApiController]
[Route("[controller]")]
[AllowAnonymous]
public class AuthController(IAuthService service) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult> Register()
    {
        var res = service.Register();
        return Ok(res);
    }

    [HttpPost]
    public async Task<ActionResult> Authenticate(LoginRequest request)
    {
        var res = await service.Authenticate(request);
        return res == null ? Problem(statusCode: 401, title: "Invalid credentials.") : Ok(res);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var res = await service.Refresh(request);
        return res == null ? Problem(statusCode: 401, title: "Invalid or expired refresh token.") : Ok(res);
    }
}
