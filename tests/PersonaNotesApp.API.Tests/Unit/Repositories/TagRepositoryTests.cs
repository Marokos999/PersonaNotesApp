using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.API.Infrastructure;
using PersonaNotesApp.API.Infrastructure.Repositories;

namespace PersonaNotesApp.API.Tests.Unit.Repositories;

public class TagRepositoryTests : IDisposable
{
  private readonly AppDbContext context;
  private readonly TagRepository sut;

  public TagRepositoryTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;
    context = new AppDbContext(options);
    sut = new TagRepository(context);
  }

  [Fact]
   public async Task GetAllTagsAsync_ReturnsAllTags()
  {
    context.Tags.AddRange(
            new Tag { Id = Guid.NewGuid(), UserId = "u1", Name = "Work" },
            new Tag { Id = Guid.NewGuid(), UserId = "u1", Name = "Personal" }
    );

    await context.SaveChangesAsync();

    var result = (await sut.GetAllTagsAsync("u1")).ToList();

    result.Should().HaveCount(2);
  }

  [Fact]
  public async Task GetTagByNameIdAsync_ReturnsTag_WhenExists()
  {
    context.Tags.Add(new Tag { Id = Guid.NewGuid(), UserId = "u1", Name = "Work" });
    await context.SaveChangesAsync();

    var result = await sut.GetTagByNameAsync("Work", "u1");

    result.Should().NotBeNull();
    result!.Name.Should().Be("Work");
  }
  [Fact]
  public async Task GetTagByNameAsync_ReturnsNull_WhenTagDoesNotExist()
  {
    var result = await sut.GetTagByNameAsync("NonExistentTag", "u1");
    result.Should().BeNull();
  }

  [Fact]
  public async Task CreateTagAsync_SavesTag()
  {
    var tag = new Tag { Id = Guid.NewGuid(), UserId = "u1", Name = "Ideas" };

    var result = await sut.CreateTagAsync(tag);

    result.Should().NotBeNull();
    context.Tags.Should().HaveCount(1);
  }

  [Fact]
  public async Task GetAllTagsAsync_ReturnsOnlyTagsOfUser_WithNonArchivedNoteCount()
  {
    var tag = new Tag { Id = Guid.NewGuid(), UserId = "u1", Name = "Work" };
    var active = new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "A", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    var archived = new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "B", ContentMarkdown = "", ContentHtml = "", IsArchived = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    active.NoteTags.Add(new NoteTag { NoteId = active.Id, TagId = tag.Id });
    archived.NoteTags.Add(new NoteTag { NoteId = archived.Id, TagId = tag.Id });
    context.Tags.AddRange(tag, new Tag { Id = Guid.NewGuid(), UserId = "u2", Name = "Other" });
    context.Notes.AddRange(active, archived);
    await context.SaveChangesAsync();

    var result = (await sut.GetAllTagsAsync("u1")).ToList();

    result.Should().ContainSingle();
    result[0].Tag.Name.Should().Be("Work");
    result[0].NoteCount.Should().Be(1);
  }

  [Fact]
  public async Task DeleteTagAsync_RemovesTagAndItsNoteLinks()
  {
    var tag = new Tag { Id = Guid.NewGuid(), UserId = "u1", Name = "Work" };
    var note = new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "A", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    note.NoteTags.Add(new NoteTag { NoteId = note.Id, TagId = tag.Id });
    context.Tags.Add(tag);
    context.Notes.Add(note);
    await context.SaveChangesAsync();

    var deleted = await sut.DeleteTagAsync(tag.Id, "u1");

    deleted.Should().BeTrue();
    context.Tags.Should().BeEmpty();
    context.NoteTags.Should().BeEmpty();
    context.Notes.Should().HaveCount(1);
  }

  [Fact]
  public async Task DeleteTagAsync_ReturnsFalse_WhenUserMismatch()
  {
    var tag = new Tag { Id = Guid.NewGuid(), UserId = "u1", Name = "Work" };
    context.Tags.Add(tag);
    await context.SaveChangesAsync();

    (await sut.DeleteTagAsync(tag.Id, "u2")).Should().BeFalse();
    context.Tags.Should().HaveCount(1);
  }

  public void Dispose() => context.Dispose();
}