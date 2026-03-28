using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Models.API.Responses;

namespace Core.Models.Entities;

public class Token
{
    [Key] public int Id { get; init; }
    public int UserId { get; init; }

    [ForeignKey(nameof(UserId))] public User User { get; set; } = null!;
    [Required] public string TokenHash { get; set; } = "";
    [Required] public string TokenFingerprint { get; set; } = "";
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);
    public DateTime? RevokedAt { get; set; }

    public bool Expired()
    {
        return RevokedAt is not null || ExpiresAt.CompareTo(DateTime.UtcNow) <= 0;
    }

    public static Token From(TokenResponse response, int userId)
    {
        return new Token
        {
            UserId = userId,
            TokenHash = response.RefreshToken,
            TokenFingerprint = response.RefreshToken
        };
    }
}
