using Core.Interface;
using Core.Models.API.Requests;
using Core.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Utils;

public class Cryptography(IRepo<User> repo)
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<(bool IsValid, User? User)> Validate(LoginRequest request)
    {
        var user = await repo.FindByUsername(request.Username);
        if (user is not null && Verify(user, request.Password)) return (true, user);

        return (false, null);
    }

    private string HashPassword(User user, string plaintext)
    {
        return _hasher.HashPassword(user, plaintext);
    }

    private bool Verify(User user, string plaintext)
    {
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, plaintext);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}