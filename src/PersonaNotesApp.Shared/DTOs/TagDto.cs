using System.ComponentModel.DataAnnotations;

namespace PersonaNotesApp.Shared.DTOs;

public class TagDto
{
    public const int MaxNameLength = 50;

    public Guid Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be in #RRGGBB format.")]
    public string Color { get; set; } = "#6B7280";

    public int NoteCount { get; set; }
}
