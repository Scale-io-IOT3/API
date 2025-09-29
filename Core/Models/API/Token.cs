using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Models.API.Responses;
using Core.Models.Entities;

namespace Core.Models.API;

public class Token
{
    [Key] public int Id { get; set; }

    [ForeignKey(nameof(Id))] public User User { get; set; } = null!;
    [Required] public string Refresh { get; set; } = "";
    public DateTime Expiry { get; set; } = DateTime.UtcNow.AddDays(30);

    public bool Expired()
    {
        return Expiry < DateTime.UtcNow;
    }

    public static Token From(LoginResponse response, int id)
    {
        return new Token
        {
            Id = id,
            Refresh = response.RefreshToken
        };
    }
}