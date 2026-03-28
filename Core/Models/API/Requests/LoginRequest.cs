using System.ComponentModel.DataAnnotations;

namespace Core.Models.API.Requests;

public class LoginRequest : Request
{
    [StringLength(64, MinimumLength = 1)]
    [Required] public required string Username { get; set; }

    [StringLength(256, MinimumLength = 1)]
    [Required] public required string Password { get; set; }
}
