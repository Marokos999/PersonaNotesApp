using System.ComponentModel.DataAnnotations;

namespace PersonaNotesApp.Shared.DTOs;

public class LoginRequest
{
    [Required, StringLength(256)]
    public string Email { get; set; } = default!;

    [Required, StringLength(100)]
    public string Password { get; set; } = default!;
}
