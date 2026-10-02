using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Application.Services;

namespace PersonaNotesApp.API.Infrastructure;

/// <summary>Re-renders stored notes whose HTML was produced by an older (or no) sanitizing renderer.</summary>
public static class NoteHtmlRefresher
{
    private const int BatchSize = 100;

    public static async Task<int> RefreshAsync(AppDbContext db)
    {
        var refreshed = 0;

        while (true)
        {
            var batch = await db.Notes
                .Where(n => n.RenderVersion < MarkdownRenderer.Version)
                .OrderBy(n => n.Id)
                .Take(BatchSize)
                .ToListAsync();
            if (batch.Count == 0) return refreshed;

            foreach (var note in batch)
            {
                note.ContentHtml = MarkdownRenderer.ToHtml(note.ContentMarkdown);
                note.RenderVersion = MarkdownRenderer.Version;
            }

            await db.SaveChangesAsync();
            refreshed += batch.Count;
        }
    }
}
