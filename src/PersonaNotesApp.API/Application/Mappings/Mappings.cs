using PersonaNotesApp.API.Application.Interfaces;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Application.Mappings;

public static class Mappings
{
    public static NoteDto ToDto(this Note note) => new()
    {
        Id = note.Id,
        Title = note.Title,
        Content = note.ContentMarkdown,
        ContentHtml = note.ContentHtml,
        IsPinned = note.IsPinned,
        IsArchived = note.IsArchived,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt,
        Tags = note.NoteTags?.Select(nt => new TagDto
        {
            Id = nt.Tag?.Id ?? Guid.Empty,
            Name = nt.Tag?.Name ?? string.Empty,
            Color = nt.Tag?.Color ?? "#6B7280"
        }).ToList() ?? []
    };

    public static TagDto ToDto(this Tag tag, int noteCount = 0) => new()
    {
        Id = tag.Id,
        Name = tag.Name,
        Color = tag.Color,
        NoteCount = noteCount
    };

    public static TagDto ToDto(this TagWithCount item) => item.Tag.ToDto(item.NoteCount);
}
