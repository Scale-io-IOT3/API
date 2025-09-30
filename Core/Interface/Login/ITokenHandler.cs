using Core.Models.API.Responses;
using Core.Models.Entities;

namespace Core.Interface.Login;

public interface ITokenHandler
{
    Task<LoginResponse> Create(User user);
    Task<RefreshResponse?> Refresh(string token);
}