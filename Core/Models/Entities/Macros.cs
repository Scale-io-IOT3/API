using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Core.Models.Entities;

public class Macros
{
    [Key] public int Id { get; init; }
    public string? FoodId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(FoodId))]
    public Food? Food { get; set; }

    public int MacroTypeId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(MacroTypeId))]
    public MacrosType? MacrosType { get; set; }

    public double Amount { get; set; }
    public double Percentage { get; set; }
}

public class MacrosType
{
    [Key] public int Id { get; init; }
    public required string Name { get; set; }
    public ICollection<Macros> Macros { get; set; } = [];
}