namespace PersonaNotesApp.API.Domain;

public class Tag
{
  public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Color { get; set; } = "#6B7280";
    public ICollection<NoteTag> NoteTags { get; set; } = [];
}