using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models.Entities;

public class Macros
{
    [Key] public int Id { get; init; }

    public required string FoodId { get; set; }

    [ForeignKey(nameof(FoodId))] public required Food Food { get; set; }

    public int MacroTypeId { get; set; }

    [ForeignKey(nameof(MacroTypeId))] public required MacrosType MacrosType { get; set; }

    public double Amount { get; set; }
    public double Percentage { get; set; }
}

public class MacrosType
{
    [Key] public int Id { get; init; }
    public required string Name { get; set; }

    public ICollection<Macros> Macros { get; set; } = [];
}