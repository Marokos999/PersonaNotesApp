using System.ComponentModel.DataAnnotations;

namespace PersonaNotesApp.Shared.DTOs;

public class RegisterRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = default!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = default!;
}
