using System.ComponentModel.DataAnnotations;

namespace PersonaNotesApp.Shared.DTOs;

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = default!;
}
