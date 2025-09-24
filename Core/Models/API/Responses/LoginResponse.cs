using System.ComponentModel.DataAnnotations;

namespace Core.Models.API.Responses;

public class LoginResponse
{
    [Required] public required string User { get; set; }
    [Required] public required string AccessToken { get; set; }
    [Required] public int ExpiresIn { get; set; }
}