using System.ComponentModel.DataAnnotations;

namespace Core.Models.API.Requests;

public class RefreshRequest
{
    [Required]
    [MinLength(32)]
    [MaxLength(256)]
    public string Token { get; set; } = "";
}
