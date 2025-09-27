using Core.Interface;
using Core.Interface.Login;
using Core.Models.API.Requests;
using Core.Models.API.Responses;
using Core.Models.Entities;
using Infrastructure.Utils;

namespace Infrastructure.Services.Login;

public class LoginService(ITokenHandler tokenHandler, IRepo<User> repo) : ILoginService
{
    private readonly Cryptography _cryptography = new(repo);

    public async Task<LoginResponse?> Authenticate(LoginRequest request)
    {
        if (!await _cryptography.Validate(request)) return null;
        var token = tokenHandler.CreateToken(request.Username, out var expiresIn);

        return new LoginResponse
        {
            AccessToken = token,
            ExpiresIn = expiresIn,
            Username = request.Username
        };
    }
}