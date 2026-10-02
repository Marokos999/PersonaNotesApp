using Carter;
using PersonaNotesApp.API.Application.Extensions;
using PersonaNotesApp.API.Application.Interfaces;
using PersonaNotesApp.API.Application.Mappings;
using PersonaNotesApp.API.Application.Services;
using PersonaNotesApp.API.Application.Validation;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.Shared.DTOs;
using System.Security.Claims;

namespace PersonaNotesApp.API.Modules;

public class NotesModule : ICarterModule
{
  public const int DefaultPageSize = 20;
  public const int MaxPageSize = 100;

  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var notesGroup = app.MapGroup("/api/notes").RequireAuthorization();

    notesGroup.MapGet("/", async (INoteRepository repo, ClaimsPrincipal user, HttpResponse response,
        string? tag, string? search, int? page, int? pageSize) =>
        {
            var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
            var result = await repo.GetAllNotesAsync(user.GetUserId(), tag, search, Math.Max(page ?? 1, 1), size);
            response.Headers["X-Total-Count"] = result.TotalCount.ToString();
            return Results.Ok(result.Items.Select(n => n.ToDto()));
        });

    notesGroup.MapGet("/{id:guid}", async (INoteRepository repo, Guid id, ClaimsPrincipal user) =>
        {
            var note = await repo.GetNoteByIdAsync(id, user.GetUserId());
            return note is null ? Results.NotFound() : Results.Ok(note.ToDto());
        });

    notesGroup.MapPost("/", async (INoteRepository repo, CreateNoteRequest request, ITagRepository tagRepo, ClaimsPrincipal user) =>
        {
            var userId = user.GetUserId();
            var note = new Note
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = request.Title.Trim(),
                ContentMarkdown = request.Content,
                ContentHtml = MarkdownRenderer.ToHtml(request.Content),
                RenderVersion = MarkdownRenderer.Version,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await AttachTagsAsync(note, request.Tags, userId, tagRepo);

            var createdNote = await repo.CreateNoteAsync(note);
            return Results.Created($"/api/notes/{createdNote.Id}", createdNote.ToDto());
        })
        .WithValidation<CreateNoteRequest>();

    notesGroup.MapPut("/{id:guid}", async (Guid id, UpdateNoteRequest request, INoteRepository repo, ITagRepository tagRepo, ClaimsPrincipal user) =>
        {
            var userId = user.GetUserId();
            var note = await repo.GetNoteByIdAsync(id, userId);
            if (note is null) return Results.NotFound();

            note.Title = request.Title.Trim();
            note.ContentMarkdown = request.Content;
            note.ContentHtml = MarkdownRenderer.ToHtml(request.Content);
            note.RenderVersion = MarkdownRenderer.Version;
            note.NoteTags.Clear();

            await AttachTagsAsync(note, request.Tags, userId, tagRepo);

            var updated = await repo.UpdateNoteAsync(note);
            return Results.Ok(updated!.ToDto());
        })
        .WithValidation<UpdateNoteRequest>();

    // Permanently deletes the note (archive is a separate toggle).
    notesGroup.MapDelete("/{id:guid}", async (Guid id, INoteRepository repo, ClaimsPrincipal user) =>
        await repo.DeleteNoteAsync(id, user.GetUserId()) ? Results.NoContent() : Results.NotFound());

    notesGroup.MapPatch("/{id:guid}/pin", async (Guid id, INoteRepository repo, ClaimsPrincipal user) =>
        {
            var note = await repo.TogglePinAsync(id, user.GetUserId());
            return note is null ? Results.NotFound() : Results.Ok(new { note.IsPinned });
        });

    notesGroup.MapPatch("/{id:guid}/archive", async (Guid id, INoteRepository repo, ClaimsPrincipal user) =>
        {
            var note = await repo.ToggleArchiveAsync(id, user.GetUserId());
            return note is null ? Results.NotFound() : Results.Ok(new { note.IsArchived });
        });

    notesGroup.MapGet("/archived", async (INoteRepository repo, ClaimsPrincipal user) =>
        {
            var notes = await repo.GetArchivedNotesAsync(user.GetUserId());
            return Results.Ok(notes.Select(n => n.ToDto()));
        });

    notesGroup.MapPatch("/{id:guid}/tags", async (Guid id, SetNoteTagsRequest request, INoteRepository repo, ClaimsPrincipal user) =>
        {
            var note = await repo.SetNoteTagsAsync(id, user.GetUserId(), request.TagIds);
            return note is null ? Results.NotFound() : Results.Ok(note.ToDto());
        });
  }

  private static async Task AttachTagsAsync(Note note, IEnumerable<string> tagNames, string userId, ITagRepository tagRepo)
  {
    var names = tagNames
      .Select(n => n?.Trim() ?? string.Empty)
      .Where(n => n.Length > 0)
      .Distinct();

    foreach (var name in names)
    {
      var tag = await tagRepo.GetTagByNameAsync(name, userId)
                ?? await tagRepo.CreateTagAsync(new Tag { Id = Guid.NewGuid(), UserId = userId, Name = name, Color = "#6B7280" });
      note.NoteTags.Add(new NoteTag { NoteId = note.Id, TagId = tag.Id, Tag = tag });
    }
  }
}
