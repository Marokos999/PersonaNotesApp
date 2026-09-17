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

    var result = await sut.GetAllTagsAsync();

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


  public void Dispose() => context.Dispose();
}