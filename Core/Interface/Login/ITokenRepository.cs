using Core.Models.Entities;

namespace Core.Interface.Login;

public interface ITokenRepository : IRepo<Token>
{
    Task<bool> TryRotate(Token current, Token replacement);
}
