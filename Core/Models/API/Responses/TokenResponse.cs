using System.ComponentModel.DataAnnotations;

namespace Core.Models.API.Responses;

public class TokenResponse : Response
{
    [Required] public required string AccessToken { get; set; }
    [Required] public required string RefreshToken { get; set; }
}