using Core.Models.API.Requests;
using Core.Models.API.Responses;

namespace Core.Interface.Login;

/// <summary>Authenticates credentials and exchanges refresh tokens without exposing persistence details.</summary>
public interface IAuthService : IService<TokenResponse, LoginRequest>
{
    /// <summary>Verifies credentials and creates an access and refresh token pair.</summary>
    /// <param name="request">The submitted login credentials.</param>
    /// <returns>The issued tokens, or null when authentication fails.</returns>
    public Task<TokenResponse?> Authenticate(LoginRequest request);
    /// <summary>Exchanges a refresh token through the token handler's rotation workflow.</summary>
    /// <param name="request">The refresh token to exchange.</param>
    /// <returns>The replacement tokens, or null when the token cannot be refreshed.</returns>
    public Task<TokenResponse?> Refresh(RefreshRequest request);

    public Task<TokenResponse?> Register();
}
