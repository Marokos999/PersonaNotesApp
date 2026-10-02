using Carter;
using PersonaNotesApp.API.Application.Extensions;
using PersonaNotesApp.API.Application.Interfaces;
using PersonaNotesApp.API.Application.Mappings;
using PersonaNotesApp.API.Application.Validation;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.Shared.DTOs;
using System.Security.Claims;

namespace PersonaNotesApp.API.Modules;

public class TagsModule : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var tagGroup = app.MapGroup("/api/tags").RequireAuthorization();

    tagGroup.MapGet("/", async (ITagRepository repo, ClaimsPrincipal user) =>
    {
      var tags = await repo.GetAllTagsAsync(user.GetUserId());
      return Results.Ok(tags.Select(t => t.ToDto()));
    });

    tagGroup.MapPost("/", async (TagDto request, ITagRepository repo, ClaimsPrincipal user) =>
    {
      var userId = user.GetUserId();
      var name = request.Name.Trim();

      if (await repo.GetTagByNameAsync(name, userId) is not null)
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Tag already exists.");

      var created = await repo.CreateTagAsync(new Tag
      {
        Id = Guid.NewGuid(),
        UserId = userId,
        Name = name,
        Color = request.Color
      });

      return Results.Created($"/api/tags/{created.Id}", created.ToDto());
    })
    .WithValidation<TagDto>();

    tagGroup.MapPut("/{id:guid}", async (Guid id, TagDto request, ITagRepository repo, ClaimsPrincipal user) =>
    {
      var userId = user.GetUserId();
      var tag = await repo.GetTagByIdAsync(id, userId);
      if (tag is null) return Results.NotFound();

      var name = request.Name.Trim();
      var existing = await repo.GetTagByNameAsync(name, userId);
      if (existing is not null && existing.Id != id)
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Tag already exists.");

      tag.Name = name;
      tag.Color = request.Color;
      return Results.Ok((await repo.UpdateTagAsync(tag)).ToDto());
    })
    .WithValidation<TagDto>();

    tagGroup.MapDelete("/{id:guid}", async (Guid id, ITagRepository repo, ClaimsPrincipal user) =>
      await repo.DeleteTagAsync(id, user.GetUserId()) ? Results.NoContent() : Results.NotFound());
  }
}
