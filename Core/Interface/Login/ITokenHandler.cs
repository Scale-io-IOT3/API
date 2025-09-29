using Core.Models.API;
using Core.Models.Entities;

namespace Core.Interface.Login;

public interface ITokenHandler
{
    Task<Token> GetOrCreate(User user);
}