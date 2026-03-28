using System.ComponentModel.DataAnnotations;

namespace Core.Models.API.Requests;

public class RefreshRequest
{
    [Required]
    [MinLength(32)]
    public string Token { get; set; } = "";
}
