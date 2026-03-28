using System.Security.Cryptography;
using Core.DTO.Auth;
using Core.Interface;
using Core.Models.API.Requests;
using Core.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Utils;

public class Cryptography(IRepo<User> repo)
{
    private static readonly PasswordHasher<User> Hasher = new();

    public async Task<UserStatus> Authenticate(LoginRequest request)
    {
        var user = await repo.FindByUsername(request.Username);
        var status = Verify(request.Password, user: user);

        return new UserStatus(status, user);
    }

    public static string Hash(string plaintext, User? user = null)
    {
        return Hasher.HashPassword(user!, plaintext);
    }

    public static bool Verify(string plaintext, string hash = "", User? user = null)
    {
        var result = Hasher.VerifyHashedPassword(user!, user?.PasswordHash ?? hash, plaintext);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    public static string GenerateToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);

        return Convert.ToBase64String(randomBytes);
    }

    public static string FingerprintToken(string plaintext)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
