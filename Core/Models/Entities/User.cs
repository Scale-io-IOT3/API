using System.ComponentModel.DataAnnotations;

namespace Core.Models.Entities;

public class User
{
    [Key] public int Id { get; init; }
    [Required] public required string Username { get; init; }
    [Required] public required string PasswordHash { get; init; }
}