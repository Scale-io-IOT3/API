using System.ComponentModel.DataAnnotations;

namespace Core.Models.Entities;

public class User
{
    [Key] public int Id { get; init; }
    [Required] public required string Username { get; set; }
    [Required] public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public ICollection<Meal> Meals { get; set; } = [];
    public ICollection<Token> RefreshTokens { get; set; } = [];
}
