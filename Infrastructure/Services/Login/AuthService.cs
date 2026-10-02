using Core.Interface;
using Core.Interface.Login;
using Core.Models.API.Requests;
using Core.Models.API.Responses;
using Core.Models.Entities;
using Infrastructure.Utils;

namespace Infrastructure.Services.Login;

/// <summary>Verifies user credentials and delegates token issuance and rotation to the token handler.</summary>
public sealed class AuthService(ITokenHandler tokenHandler, IRepo<User> repo) : IAuthService
{
    private readonly Cryptography _cryptography = new(repo);

    /// <inheritdoc />
    public async Task<TokenResponse?> Authenticate(LoginRequest request)
    {
        var status = await _cryptography.Authenticate(request);
        if (!status.Valid()) return null;

        return await tokenHandler.Create(status.User!);
    }

    /// <inheritdoc />
    public Task<TokenResponse?> Refresh(RefreshRequest request)
    {
        return tokenHandler.Refresh(request.Token);
    }
}
