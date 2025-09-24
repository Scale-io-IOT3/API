using Core.Models.API.Requests;
using Core.Models.API.Responses;

namespace Core.Interface.Login;

public interface ILoginService : IService<LoginResponse, LoginRequest>
{
    public Task<LoginResponse?> Authenticate(LoginRequest request);
}