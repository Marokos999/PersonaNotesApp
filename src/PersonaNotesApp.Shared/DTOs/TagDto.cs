namespace PersonaNotesApp.Shared.DTOs;

public class TagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6B7280";
    public int NoteCount { get; set; }
}
