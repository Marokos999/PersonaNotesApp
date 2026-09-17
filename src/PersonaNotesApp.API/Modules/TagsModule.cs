using Carter;
using PersonaNotesApp.API.Application.Interfaces;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.Shared.DTOs;
using System.Security.Claims;

namespace PersonaNotesApp.API.Modules;

public class TagsModule : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var tagGroup = app.MapGroup("/api/tags").RequireAuthorization();

    tagGroup.MapGet("/", async (ITagRepository repo, HttpContext httpContext) =>
    {
      var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
      var tags = await repo.GetAllTagsAsync(userId);
      return Results.Ok(tags.Select(t => new TagDto
      {
        Id = t.Id,
        Name = t.Name,
        Color = t.Color
      }));
    });

    tagGroup.MapPost("/", async (TagDto request, ITagRepository repo, HttpContext httpContext) =>
    {
      var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
      var tag = new Tag
      {
        Id = Guid.NewGuid(),
        UserId = userId,
        Name = request.Name,
        Color = request.Color
      };

      var created = await repo.CreateTagAsync(tag);
      return Results.Created($"/api/tags/{created.Id}", new TagDto
      {
        Id = created.Id,
        Name = created.Name,
        Color = created.Color
      });
    });
  }
}