using System.ComponentModel.DataAnnotations;

namespace PersonaNotesApp.Shared.DTOs;

public class DeleteAccountRequest
{
    /// <summary>The current password, required so a stolen access token alone cannot erase an account.</summary>
    [Required, StringLength(100)]
    public string Password { get; set; } = default!;
}
