using Core.Models.Entities;

namespace Core.DTO.Auth;

public class UserStatus(bool status, User? user)
{
    private bool AuthStatus { get; } = status;

    public User? User { get; } = user;

    public bool Valid()
    {
        return AuthStatus && User is not null;
    }
}