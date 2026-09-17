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

  public async Task<IEnumerable<Tag>> GetAllTagsAsync(string? userId = null) =>
    await context.Tags
        .Where(t => userId == null || t.UserId == userId)
        .ToListAsync();

  public async Task<Tag?> GetTagByNameAsync(string name, string userId)
  {
    return await context.Tags.FirstOrDefaultAsync(t => t.Name == name && t.UserId == userId );
  }

}