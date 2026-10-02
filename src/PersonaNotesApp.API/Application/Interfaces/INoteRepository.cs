using PersonaNotesApp.API.Domain;

namespace PersonaNotesApp.API.Application.Interfaces;

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

public interface INoteRepository
{
  Task<PagedResult<Note>> GetAllNotesAsync(string userId, string? tag = null, string? search = null, int page = 1, int pageSize = 20);
  Task<Note?> GetNoteByIdAsync(Guid id, string userId);
  Task<Note> CreateNoteAsync(Note note);
  Task<Note?> UpdateNoteAsync(Note note);
  Task<bool> DeleteNoteAsync(Guid id, string userId);
  Task<Note?> TogglePinAsync(Guid id, string userId);
  Task<Note?> ToggleArchiveAsync(Guid id, string userId);
  Task<IEnumerable<Note>> GetArchivedNotesAsync(string userId);
  Task<Note?> SetNoteTagsAsync(Guid id, string userId, IEnumerable<Guid> tagIds);
}
