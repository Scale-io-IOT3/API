using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Models.API.Responses;

namespace Core.Models.Entities;

public class Token
{
    [Key] public int Id { get; set; }

    [ForeignKey(nameof(Id))] public User User { get; set; } = null!;
    [Required] public string Refresh { get; set; } = "";
    public DateTime Expiry { get; set; } = DateTime.UtcNow.AddDays(30);

    public bool Expired()
    {
        return Expiry.CompareTo(DateTime.UtcNow) <= 0;
    }

    public static Token From(TokenResponse response, int id)
    {
        return new Token
        {
            Id = id,
            Refresh = response.RefreshToken
        };
    }
}