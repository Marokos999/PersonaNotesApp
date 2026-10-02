using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Application.Interfaces;
using PersonaNotesApp.API.Domain;

namespace PersonaNotesApp.API.Infrastructure.Repositories;

public class TagRepository(AppDbContext context) : ITagRepository
{
  public async Task<Tag> CreateTagAsync(Tag tag)
  {
    context.Tags.Add(tag);
    await context.SaveChangesAsync();
    return tag;
  }

  public async Task<IEnumerable<TagWithCount>> GetAllTagsAsync(string userId) =>
    await context.Tags
        .AsNoTracking()
        .Where(t => t.UserId == userId)
        .OrderBy(t => t.Name)
        .Select(t => new TagWithCount(t, t.NoteTags.Count(nt => !nt.Note.IsArchived)))
        .ToListAsync();

  public async Task<Tag?> GetTagByIdAsync(Guid id, string userId) =>
    await context.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

  public async Task<Tag?> GetTagByNameAsync(string name, string userId) =>
    await context.Tags.FirstOrDefaultAsync(t => t.Name == name && t.UserId == userId);

  public async Task<Tag> UpdateTagAsync(Tag tag)
  {
    context.Tags.Update(tag);
    await context.SaveChangesAsync();
    return tag;
  }

  public async Task<bool> DeleteTagAsync(Guid id, string userId)
  {
    var tag = await context.Tags
      .Include(t => t.NoteTags)
      .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    if (tag is null) return false;

    context.Tags.Remove(tag);
    await context.SaveChangesAsync();
    return true;
  }
}
