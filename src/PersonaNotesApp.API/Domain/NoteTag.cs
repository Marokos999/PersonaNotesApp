namespace PersonaNotesApp.API.Domain;

public class NoteTag
{
  public Guid NoteId { get; set; }
    public Guid TagId { get; set; }
    public Note Note { get; set; } = default!;
    public Tag Tag { get; set; } = default!;
}