using Core.Models.API.Responses;
using Core.Models.Entities;

namespace Core.Interface.Login;

public interface ITokenHandler
{
    Task<TokenResponse> Create(User user);
    Task<TokenResponse?> Refresh(string token);
}