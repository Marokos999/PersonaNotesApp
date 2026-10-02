using PersonaNotesApp.API.Domain;

namespace PersonaNotesApp.API.Application.Interfaces;

public record TagWithCount(Tag Tag, int NoteCount);

public interface ITagRepository
{
  Task<IEnumerable<TagWithCount>> GetAllTagsAsync(string userId);
  Task<Tag?> GetTagByIdAsync(Guid id, string userId);
  Task<Tag?> GetTagByNameAsync(string name, string userId);
  Task<Tag> CreateTagAsync(Tag tag);
  Task<Tag> UpdateTagAsync(Tag tag);
  Task<bool> DeleteTagAsync(Guid id, string userId);
}
