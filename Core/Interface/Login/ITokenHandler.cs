namespace Core.Interface.Login;

public interface ITokenHandler
{
    string CreateToken(string username, out int expiresIn);
}