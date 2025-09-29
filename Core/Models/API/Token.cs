using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Models.Entities;

namespace Core.Models.API;

public class Token
{
    [Key] public int Id { get; set; }

    [ForeignKey(nameof(Id))] public User User { get; set; } = null!;
    [Required] public string Access { get; set; } = "";
    [Required] public string Refresh { get; set; } = "";
    public DateTime RefreshExpiry { get; set; } = DateTime.UtcNow.AddDays(30);
    public DateTime AccessExpiry { get; init; }
}