namespace PersonaNotesApp.Shared.DTOs;

/// <summary>Everything the app stores about a user, in a portable format (GDPR data export).</summary>
public class AccountExportDto
{
    public DateTime ExportedAt { get; set; }
    public string Email { get; set; } = string.Empty;
    public List<ExportedTagDto> Tags { get; set; } = [];
    public List<ExportedNoteDto> Notes { get; set; } = [];
}

public class ExportedTagDto
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}

public class ExportedNoteDto
{
    public string Title { get; set; } = string.Empty;
    /// <summary>The note as written, in Markdown.</summary>
    public string Content { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Tags { get; set; } = [];
}
