namespace PersonaNotesApp.Shared.DTOs;

public class SetNoteTagsRequest
{
    public List<Guid> TagIds { get; set; } = [];
}
