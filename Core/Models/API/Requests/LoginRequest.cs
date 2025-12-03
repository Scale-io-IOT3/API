using System.ComponentModel.DataAnnotations;

namespace Core.Models.API.Requests;

public class LoginRequest : Request
{
    [Required] public required string Username { get; set; }
    [Required] public required string Password { get; set; }
}