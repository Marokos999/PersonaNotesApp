using PersonaNotesApp.API.Domain;

namespace PersonaNotesApp.API.Application.Interfaces;

public interface ITagRepository
{
  Task<IEnumerable<Tag>> GetAllTagsAsync(string? userId = null);
  Task<Tag?> GetTagByNameAsync(string name, string userId);
  Task<Tag> CreateTagAsync(Tag tag);
}