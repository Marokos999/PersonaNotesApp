using System.ComponentModel.DataAnnotations;

namespace PersonaNotesApp.Shared.DTOs;

public abstract class NoteWriteRequest : IValidatableObject
{
    public const int MaxTitleLength = 200;
    public const int MaxContentLength = 100_000;
    public const int MaxTags = 20;

    [Required(AllowEmptyStrings = false)]
    [StringLength(MaxTitleLength)]
    public string Title { get; set; } = string.Empty;

    [StringLength(MaxContentLength)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(MaxTags)]
    public List<string> Tags { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Tags.Any(t => t is not null && t.Trim().Length > TagDto.MaxNameLength))
            yield return new ValidationResult(
                $"Tag name must be at most {TagDto.MaxNameLength} characters.", [nameof(Tags)]);
    }
}
