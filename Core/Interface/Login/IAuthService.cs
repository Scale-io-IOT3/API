using Core.Models.API.Requests;
using Core.Models.API.Responses;

namespace Core.Interface.Login;

public interface IAuthService : IService<TokenResponse, LoginRequest>
{
    public Task<TokenResponse?> Authenticate(LoginRequest request);
    public Task<TokenResponse?> Refresh(RefreshRequest request);
}