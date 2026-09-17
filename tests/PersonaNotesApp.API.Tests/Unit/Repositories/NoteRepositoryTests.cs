using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.API.Infrastructure;
using PersonaNotesApp.API.Infrastructure.Repositories;

namespace PersonaNotesApp.API.Tests.Unit.Repositories;

public class NoteRepositoryTests : IDisposable
{
    private readonly AppDbContext context;
    private readonly NoteRepository sut;
  public NoteRepositoryTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;
    context = new AppDbContext(options);
    sut = new NoteRepository(context);
  }

  [Fact]
  public async Task GetAllNotesAync_ReturnsOnlyNonArchived()
  {
    context.Notes.AddRange(
            new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "Active", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "Archived", ContentMarkdown = "", ContentHtml = "", IsArchived = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }

    );

    await context.SaveChangesAsync();

    var result = await sut.GetAllNotesAsync();

    result.Should().HaveCount(1);
    result.First().Title.Should().Be("Active");
  }

  [Fact]
  public async Task GetNoteByIdAsync_ReturnsNote_WhenExists()
  {
    var id = Guid.NewGuid();
    context.Notes.Add(new Note { Id = id, UserId = "u1", Title = "Test Note", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    await context.SaveChangesAsync();

    var result = await sut.GetNoteByIdAsync(id);

    result.Should().NotBeNull();
    result!.Title.Should().Be("Test Note");
  }

  [Fact]
  public async Task GetNoteByIdAsync_ReturnsNull_WhenNoteDoesNotExist()
  {
    var result = await sut.GetNoteByIdAsync(Guid.NewGuid());
    result.Should().BeNull();
  }

  [Fact]
  public async Task CreateNoteAsync_SaveNote()
  {
    var note = new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "New Note", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    var result = await sut.CreateNoteAsync(note);

    result.Should().NotBeNull();
    context.Notes.Should().HaveCount(1);
  }

  [Fact]
  public async Task UpdateNoteAsync_UpdatesNote()
  {
    var note = new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "Old Title", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    context.Notes.Add(note);
    await context.SaveChangesAsync();

    note.Title = "Updated Title";
    await sut.UpdateNoteAsync(note);

    var updatedNote = await context.Notes.FindAsync(note.Id);
    updatedNote!.Title.Should().Be("Updated Title");
  }

  [Fact]
  public async Task DeleteNoteAsync_SetsIsArchived()
  {
    var id = Guid.NewGuid();
    context.Notes.Add(new Note { Id = id, UserId = "u1", Title = "ToDelete", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    await context.SaveChangesAsync();

    await sut.DeleteNoteAsync(id);

    var deletedNote = await context.Notes.FindAsync(id);
    deletedNote!.IsArchived.Should().BeTrue();
  }

  [Fact]
  public async Task TogglePinAsync_PinsNote_WhenNotPinned()
  {
    var id = Guid.NewGuid();
    context.Notes.Add(new Note { Id = id, UserId = "u1", Title = "T", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    await context.SaveChangesAsync();

    var result = await sut.TogglePinAsync(id, "u1");

    result.Should().NotBeNull();
    result!.IsPinned.Should().BeTrue();
  }

  [Fact]
  public async Task TogglePinAsync_UnpinsNote_WhenAlreadyPinned()
  {
    var id = Guid.NewGuid();
    context.Notes.Add(new Note { Id = id, UserId = "u1", Title = "T", ContentMarkdown = "", ContentHtml = "", IsPinned = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    await context.SaveChangesAsync();

    var result = await sut.TogglePinAsync(id, "u1");

    result!.IsPinned.Should().BeFalse();
  }

  [Fact]
  public async Task TogglePinAsync_ReturnsNull_WhenNoteNotFound()
  {
    var result = await sut.TogglePinAsync(Guid.NewGuid(), "u1");
    result.Should().BeNull();
  }

  [Fact]
  public async Task TogglePinAsync_ReturnsNull_WhenUserMismatch()
  {
    var id = Guid.NewGuid();
    context.Notes.Add(new Note { Id = id, UserId = "u1", Title = "T", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    await context.SaveChangesAsync();

    var result = await sut.TogglePinAsync(id, "other-user");

    result.Should().BeNull();
  }

  [Fact]
  public async Task ToggleArchiveAsync_ArchivesNote_WhenNotArchived()
  {
    var id = Guid.NewGuid();
    context.Notes.Add(new Note { Id = id, UserId = "u1", Title = "T", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    await context.SaveChangesAsync();

    var result = await sut.ToggleArchiveAsync(id, "u1");

    result!.IsArchived.Should().BeTrue();
  }

  [Fact]
  public async Task ToggleArchiveAsync_RestoresNote_WhenAlreadyArchived()
  {
    var id = Guid.NewGuid();
    context.Notes.Add(new Note { Id = id, UserId = "u1", Title = "T", ContentMarkdown = "", ContentHtml = "", IsArchived = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    await context.SaveChangesAsync();

    var result = await sut.ToggleArchiveAsync(id, "u1");

    result!.IsArchived.Should().BeFalse();
  }

  [Fact]
  public async Task GetArchivedNotesAsync_ReturnsOnlyArchivedForUser()
  {
    context.Notes.AddRange(
      new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "Active", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
      new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "Archived", ContentMarkdown = "", ContentHtml = "", IsArchived = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
      new Note { Id = Guid.NewGuid(), UserId = "u2", Title = "Other user archived", ContentMarkdown = "", ContentHtml = "", IsArchived = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
    );
    await context.SaveChangesAsync();

    var result = await sut.GetArchivedNotesAsync("u1");

    result.Should().HaveCount(1);
    result.First().Title.Should().Be("Archived");
  }

  [Fact]
  public async Task SetNoteTagsAsync_ReplacesExistingTags()
  {
    var noteId = Guid.NewGuid();
    var tag1 = new Tag { Id = Guid.NewGuid(), UserId = "u1", Name = "Old", Color = "#000" };
    var tag2 = new Tag { Id = Guid.NewGuid(), UserId = "u1", Name = "New", Color = "#fff" };
    var note = new Note
    {
      Id = noteId, UserId = "u1", Title = "T", ContentMarkdown = "", ContentHtml = "",
      CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
      NoteTags = [new NoteTag { NoteId = noteId, TagId = tag1.Id }]
    };
    context.Tags.AddRange(tag1, tag2);
    context.Notes.Add(note);
    await context.SaveChangesAsync();

    var result = await sut.SetNoteTagsAsync(noteId, "u1", [tag2.Id]);

    result.Should().NotBeNull();
    result!.NoteTags.Should().HaveCount(1);
    result.NoteTags.First().TagId.Should().Be(tag2.Id);
  }

  [Fact]
  public async Task SetNoteTagsAsync_ReturnsNull_WhenUserMismatch()
  {
    var noteId = Guid.NewGuid();
    context.Notes.Add(new Note { Id = noteId, UserId = "u1", Title = "T", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    await context.SaveChangesAsync();

    var result = await sut.SetNoteTagsAsync(noteId, "other-user", []);

    result.Should().BeNull();
  }

  [Fact]
  public async Task GetAllNotesAsync_ReturnsPinnedFirst()
  {
    context.Notes.AddRange(
      new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "Regular", ContentMarkdown = "", ContentHtml = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow.AddMinutes(-1) },
      new Note { Id = Guid.NewGuid(), UserId = "u1", Title = "Pinned", ContentMarkdown = "", ContentHtml = "", IsPinned = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
    );
    await context.SaveChangesAsync();

    var result = (await sut.GetAllNotesAsync()).ToList();

    result[0].Title.Should().Be("Pinned");
    result[1].Title.Should().Be("Regular");
  }

  public void Dispose() => context.Dispose();
}