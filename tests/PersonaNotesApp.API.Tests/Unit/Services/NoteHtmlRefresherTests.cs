using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Application.Services;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.API.Infrastructure;

namespace PersonaNotesApp.API.Tests.Unit.Services;

public class NoteHtmlRefresherTests : IDisposable
{
    private readonly AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    public void Dispose() => db.Dispose();

    private static Note NewNote(string markdown, string html, int version) => new()
    {
        Id = Guid.NewGuid(), UserId = "u1", Title = "T", ContentMarkdown = markdown, ContentHtml = html,
        RenderVersion = version, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task ReRendersOutdatedNotes_WithSanitizedHtml()
    {
        var old = NewNote("hi <script>alert(1)</script>", "<p>hi <script>alert(1)</script></p>", version: 0);
        db.Notes.Add(old);
        await db.SaveChangesAsync();

        var count = await NoteHtmlRefresher.RefreshAsync(db);

        count.Should().Be(1);
        var stored = await db.Notes.SingleAsync();
        stored.ContentHtml.Should().NotContain("<script");
        stored.RenderVersion.Should().Be(MarkdownRenderer.Version);
    }

    [Fact]
    public async Task LeavesCurrentNotesUntouched_AndHandlesMoreThanOneBatch()
    {
        var current = NewNote("x", "<p>kept as is</p>", MarkdownRenderer.Version);
        db.Notes.Add(current);
        for (var i = 0; i < 150; i++) db.Notes.Add(NewNote($"note {i}", "", version: 0));
        await db.SaveChangesAsync();

        var count = await NoteHtmlRefresher.RefreshAsync(db);

        count.Should().Be(150);
        (await db.Notes.FindAsync(current.Id))!.ContentHtml.Should().Be("<p>kept as is</p>");
        (await db.Notes.CountAsync(n => n.RenderVersion < MarkdownRenderer.Version)).Should().Be(0);
    }
}
