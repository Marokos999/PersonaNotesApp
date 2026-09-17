using PersonaNotesApp.API.Domain;

namespace PersonaNotesApp.API.Application.Interfaces;

public interface INoteRepository
{
  Task<IEnumerable<Note>> GetAllNotesAsync(string? tag = null, string? search = null, string? userId = null);
  Task<Note?> GetNoteByIdAsync(Guid id, string? userId = null);
  Task<Note> CreateNoteAsync(Note note);
  Task<Note?> UpdateNoteAsync(Note note);
  Task<bool> DeleteNoteAsync(Guid id);
  Task<Note?> TogglePinAsync(Guid id, string userId);
  Task<Note?> ToggleArchiveAsync(Guid id, string userId);
  Task<IEnumerable<Note>> GetArchivedNotesAsync(string userId);
  Task<Note?> SetNoteTagsAsync(Guid id, string userId, IEnumerable<Guid> tagIds);
}