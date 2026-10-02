using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Application.Interfaces;
using PersonaNotesApp.API.Domain;

namespace PersonaNotesApp.API.Infrastructure.Repositories;

public class NoteRepository(AppDbContext context) : INoteRepository
{
  public async Task<Note> CreateNoteAsync(Note note)
  {
    context.Notes.Add(note);
    await context.SaveChangesAsync();
    return note;
  }

  public async Task<bool> DeleteNoteAsync(Guid id, string userId)
  {
    var note = await context.Notes
      .Include(n => n.NoteTags)
      .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    if (note is null) return false;

    context.Notes.Remove(note);
    await context.SaveChangesAsync();
    return true;
  }

  public async Task<PagedResult<Note>> GetAllNotesAsync(
    string userId, string? tag = null, string? search = null, int page = 1, int pageSize = 20)
  {
    var query = context.Notes
                .AsNoTracking()
                .Where(n => !n.IsArchived && n.UserId == userId);

    if (!string.IsNullOrWhiteSpace(tag))
      query = query.Where(n => n.NoteTags.Any(nt => nt.Tag.Name == tag));

    if (!string.IsNullOrWhiteSpace(search))
    {
      query = context.Database.IsNpgsql()
        ? query.Where(n => n.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("simple", search)))
        : query.Where(n => n.Title.Contains(search) || n.ContentMarkdown.Contains(search));
    }

    var total = await query.CountAsync();

    var items = await query
                .Include(n => n.NoteTags)
                .ThenInclude(nt => nt.Tag)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

    return new PagedResult<Note>(items, total);
  }

  public async Task<Note?> GetNoteByIdAsync(Guid id, string userId) =>
   await context.Notes
                .Include(n => n.NoteTags)
                .ThenInclude(nt => nt.Tag)
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

  public async Task<Note?> UpdateNoteAsync(Note note)
  {
    note.UpdatedAt = DateTime.UtcNow;
    context.Notes.Update(note);
    await context.SaveChangesAsync();
    return note;
  }

  public async Task<Note?> TogglePinAsync(Guid id, string userId)
  {
    var note = await context.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    if (note is null) return null;
    note.IsPinned = !note.IsPinned;
    note.UpdatedAt = DateTime.UtcNow;
    await context.SaveChangesAsync();
    return note;
  }

  public async Task<Note?> ToggleArchiveAsync(Guid id, string userId)
  {
    var note = await context.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    if (note is null) return null;
    note.IsArchived = !note.IsArchived;
    note.UpdatedAt = DateTime.UtcNow;
    await context.SaveChangesAsync();
    return note;
  }

  public async Task<IEnumerable<Note>> GetArchivedNotesAsync(string userId) =>
    await context.Notes
      .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
      .Where(n => n.IsArchived && n.UserId == userId)
      .OrderByDescending(n => n.UpdatedAt)
      .ToListAsync();

  public async Task<Note?> SetNoteTagsAsync(Guid id, string userId, IEnumerable<Guid> tagIds)
  {
    var note = await context.Notes
      .Include(n => n.NoteTags)
      .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    if (note is null) return null;

    // Only tags owned by the same user may be attached.
    var requested = tagIds.Distinct().ToList();
    var ownedTagIds = await context.Tags
      .Where(t => t.UserId == userId && requested.Contains(t.Id))
      .Select(t => t.Id)
      .ToListAsync();

    note.NoteTags.Clear();
    foreach (var tagId in ownedTagIds)
      note.NoteTags.Add(new NoteTag { NoteId = id, TagId = tagId });

    note.UpdatedAt = DateTime.UtcNow;
    await context.SaveChangesAsync();

    return await context.Notes
      .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
      .FirstOrDefaultAsync(n => n.Id == id);
  }

}
