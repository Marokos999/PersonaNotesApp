using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.API.Infrastructure;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Application.Services;

/// <summary>Account-level data operations: export everything a user owns, or erase the account and all of it.</summary>
public class AccountService(AppDbContext db, UserManager<ApplicationUser> users)
{
    public async Task<AccountExportDto> ExportAsync(ApplicationUser user)
    {
        var tags = await db.Tags.AsNoTracking()
            .Where(t => t.UserId == user.Id)
            .OrderBy(t => t.Name)
            .Select(t => new ExportedTagDto { Name = t.Name, Color = t.Color })
            .ToListAsync();

        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.UserId == user.Id)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new ExportedNoteDto
            {
                Title = n.Title,
                Content = n.ContentMarkdown,
                IsPinned = n.IsPinned,
                IsArchived = n.IsArchived,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                Tags = n.NoteTags.Select(nt => nt.Tag.Name).OrderBy(name => name).ToList()
            })
            .ToListAsync();

        return new AccountExportDto
        {
            ExportedAt = DateTime.UtcNow,
            Email = user.Email ?? string.Empty,
            Tags = tags,
            Notes = notes
        };
    }

    /// <summary>
    /// Removes the user's notes, tags, refresh tokens and the account itself. Everything is staged on the context and
    /// written by the single save inside <see cref="UserManager{TUser}.DeleteAsync"/>, so it succeeds or fails as a whole.
    /// </summary>
    public async Task<IdentityResult> DeleteAsync(ApplicationUser user)
    {
        db.NoteTags.RemoveRange(await db.NoteTags.Where(nt => nt.Note.UserId == user.Id).ToListAsync());
        db.Notes.RemoveRange(await db.Notes.Where(n => n.UserId == user.Id).ToListAsync());
        db.Tags.RemoveRange(await db.Tags.Where(t => t.UserId == user.Id).ToListAsync());
        db.RefreshTokens.RemoveRange(await db.RefreshTokens.Where(r => r.UserId == user.Id).ToListAsync());

        return await users.DeleteAsync(user);
    }
}
