using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Core.Models.Entities;

public class Meal
{
    [Key] public int Id { get; init; }
    public int UserId { get; init; }

    [JsonIgnore][ForeignKey(nameof(UserId))]
    public User? User { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow.Date;

    public ICollection<Food> Foods { get; set; } = [];
}