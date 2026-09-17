using Carter;
using Markdig;
using PersonaNotesApp.API.Application.Interfaces;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.Shared.DTOs;
using System.Security.Claims;

namespace PersonaNotesApp.API.Modules;

public class NotesModule : ICarterModule
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseTaskLists()
        .Build();

  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var notesGroup = app.MapGroup("/api/notes").RequireAuthorization();

    notesGroup.MapGet("/", async (INoteRepository repo, HttpContext httpContext, string? tag, string? search) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var notes = await repo.GetAllNotesAsync(tag, search, userId);
            return Results.Ok(notes.Select(MapToDto));
        });

    notesGroup.MapGet("/{id:guid}", async (INoteRepository repo, Guid id, HttpContext httpContext) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var note = await repo.GetNoteByIdAsync(id, userId);
            return note is null ? Results.NotFound() : Results.Ok(MapToDto(note));
        });
    notesGroup.MapPost("/", async (INoteRepository repo, CreateNoteRequest request, ITagRepository tagRepo, HttpContext httpContext) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var note = new Note
            {
                Id = Guid.NewGuid(),
                UserId = userId!,
                Title = request.Title,
                ContentMarkdown = request.Content,
                ContentHtml = Markdown.ToHtml(request.Content, Pipeline),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var tagName in request.Tags)
            {
                var tag = await tagRepo.GetTagByNameAsync(tagName, userId!)
                    ?? await tagRepo.CreateTagAsync(new Tag { Id = Guid.NewGuid(), UserId = userId!, Name = tagName, Color = "#6B7280" });
                note.NoteTags.Add(new NoteTag { NoteId = note.Id, TagId = tag.Id });
            }

            var createdNote = await repo.CreateNoteAsync(note);
            return Results.Created($"/api/notes/{createdNote.Id}", MapToDto(createdNote));
        });
    notesGroup.MapPut("/{id:guid}", async (Guid id, UpdateNoteRequest request, INoteRepository repo, ITagRepository tagRepo, HttpContext httpContext) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var note = await repo.GetNoteByIdAsync(id, userId);
            if (note is null) return Results.NotFound();

            note.Title = request.Title;
            note.ContentMarkdown = request.Content;
            note.ContentHtml = Markdown.ToHtml(request.Content, Pipeline);
            note.NoteTags.Clear();

            foreach (var tagName in request.Tags)
            {
                var tag = await tagRepo.GetTagByNameAsync(tagName, userId!)
                          ?? await tagRepo.CreateTagAsync(new Tag { Id = Guid.NewGuid(), UserId = userId!, Name = tagName });
                note.NoteTags.Add(new NoteTag { NoteId = note.Id, TagId = tag.Id });
            }

            var updated = await repo.UpdateNoteAsync(note);
            return Results.Ok(MapToDto(updated!));
        });

        notesGroup.MapDelete("/{id:guid}", async (Guid id, INoteRepository repo, HttpContext httpContext) =>
        {
           var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
           var note = await repo.GetNoteByIdAsync(id, userId);
           if (note is null) return Results.NotFound();
           await repo.DeleteNoteAsync(id);
           return Results.NoContent();
        });

        notesGroup.MapPatch("/{id:guid}/pin", async (Guid id, INoteRepository repo, HttpContext httpContext) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var note = await repo.TogglePinAsync(id, userId);
            return note is null ? Results.NotFound() : Results.Ok(new { note.IsPinned });
        });

        notesGroup.MapPatch("/{id:guid}/archive", async (Guid id, INoteRepository repo, HttpContext httpContext) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var note = await repo.ToggleArchiveAsync(id, userId);
            return note is null ? Results.NotFound() : Results.Ok(new { note.IsArchived });
        });

        notesGroup.MapGet("/archived", async (INoteRepository repo, HttpContext httpContext) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var notes = await repo.GetArchivedNotesAsync(userId);
            return Results.Ok(notes.Select(MapToDto));
        });

        notesGroup.MapPatch("/{id:guid}/tags", async (Guid id, SetNoteTagsRequest request, INoteRepository repo, HttpContext httpContext) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var note = await repo.SetNoteTagsAsync(id, userId, request.TagIds);
            return note is null ? Results.NotFound() : Results.Ok(MapToDto(note));
        });
    }

 private static NoteDto MapToDto(Note note) => new()
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
}
