using NpgsqlTypes;

namespace PersonaNotesApp.API.Domain;

public class Note
{
  public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string ContentMarkdown { get; set; } = default!;
    public string ContentHtml { get; set; } = default!;     // pre-rendered
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int RenderVersion { get; set; }                       // MarkdownRenderer.Version used for ContentHtml
    public NpgsqlTsVector SearchVector { get; set; } = default!;   // generated column (PostgreSQL only)
    public ICollection<NoteTag> NoteTags { get; set; } = [];
}