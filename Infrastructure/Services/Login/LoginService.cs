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
        var (valid, user) = await _cryptography.Validate(request);
        if (!valid || user is null) return null;

        var token = await tokenHandler.GetOrCreate(user);

        return new LoginResponse
        {
            AccessToken = token.Access,
            RefreshToken = token.Refresh,
            ExpiresIn = (int)(token.AccessExpiry - DateTime.UtcNow).TotalSeconds,
            Username = user.Username
        };
    }
}