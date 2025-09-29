using Core.Interface;
using Core.Interface.Login;
using Core.Models.API.Requests;
using Core.Models.API.Responses;
using Core.Models.Entities;
using Infrastructure.Utils;

namespace Infrastructure.Services.Login;

public class AuthService(ITokenHandler tokenHandler, IRepo<User> repo) : IAuthService
{
    private readonly Cryptography _cryptography = new(repo);

    public async Task<LoginResponse?> Authenticate(LoginRequest request)
    {
        var status = await _cryptography.Authenticate(request);
        if (!status.Valid()) return null;

        var token = await tokenHandler.Create(status.User!);

        return new LoginResponse
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            Username = request.Username
        };
    }
}