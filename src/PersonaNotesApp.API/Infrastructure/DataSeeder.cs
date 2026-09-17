using Markdig;
using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Domain;

namespace PersonaNotesApp.API.Infrastructure;

public static class DataSeeder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseTaskLists()
        .Build();

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Notes.AnyAsync()) return;

        var tagWork = new Tag { Id = Guid.NewGuid(), UserId = "default", Name = "Work", Color = "#3B82F6" };
        var tagPersonal = new Tag { Id = Guid.NewGuid(), UserId = "default", Name = "Personal", Color = "#10B981" };
        var tagIdeas = new Tag { Id = Guid.NewGuid(), UserId = "default", Name = "Ideas", Color = "#F59E0B" };

        db.Tags.AddRange(tagWork, tagPersonal, tagIdeas);

        var notes = new[]
        {
            new Note
            {
                Id = Guid.NewGuid(),
                UserId = "default",
                Title = "Getting Started",
                ContentMarkdown = "# Welcome to PersonaNotes\n\nThis is your **personal notes app**. You can:\n\n- Create and edit notes in Markdown\n- Organize with tags\n- Search across all notes\n- Pin important notes",
                ContentHtml = Markdown.ToHtml("# Welcome to PersonaNotes\n\nThis is your **personal notes app**. You can:\n\n- Create and edit notes in Markdown\n- Organize with tags\n- Search across all notes\n- Pin important notes", Pipeline),
                IsPinned = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Note
            {
                Id = Guid.NewGuid(),
                UserId = "default",
                Title = "Project Ideas",
                ContentMarkdown = "## Ideas to explore\n\n1. Build a habit tracker\n2. Create a workout logger\n3. Design a portfolio site\n\n> Start with the simplest version first.",
                ContentHtml = Markdown.ToHtml("## Ideas to explore\n\n1. Build a habit tracker\n2. Create a workout logger\n3. Design a portfolio site\n\n> Start with the simplest version first.", Pipeline),
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Note
            {
                Id = Guid.NewGuid(),
                UserId = "default",
                Title = "Meeting Notes",
                ContentMarkdown = "## Sprint planning\n\n**Date:** Today\n\n### Action items\n- [ ] Set up CI/CD pipeline\n- [ ] Review pull requests\n- [ ] Update documentation",
                ContentHtml = Markdown.ToHtml("## Sprint planning\n\n**Date:** Today\n\n### Action items\n- [ ] Set up CI/CD pipeline\n- [ ] Review pull requests\n- [ ] Update documentation", Pipeline),
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        notes[0].NoteTags.Add(new NoteTag { NoteId = notes[0].Id, TagId = tagPersonal.Id });
        notes[1].NoteTags.Add(new NoteTag { NoteId = notes[1].Id, TagId = tagIdeas.Id });
        notes[2].NoteTags.Add(new NoteTag { NoteId = notes[2].Id, TagId = tagWork.Id });

        db.Notes.AddRange(notes);
        await db.SaveChangesAsync();
    }
}
