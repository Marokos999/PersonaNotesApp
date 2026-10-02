using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Domain;

namespace PersonaNotesApp.API.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<NoteTag> NoteTags => Set<NoteTag>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NoteTag>()
            .HasKey(nt => new { nt.NoteId, nt.TagId });

        modelBuilder.Entity<NoteTag>()
            .HasOne(nt => nt.Note)
            .WithMany(n => n.NoteTags)
            .HasForeignKey(nt => nt.NoteId);

        modelBuilder.Entity<NoteTag>()
            .HasOne(nt => nt.Tag)
            .WithMany(t => t.NoteTags)
            .HasForeignKey(nt => nt.TagId);

        modelBuilder.Entity<Note>()
            .HasIndex(n => n.UserId);

        // Full-text search is PostgreSQL-specific; other providers (in-memory tests) skip the column.
        if (Database.IsNpgsql())
        {
            modelBuilder.Entity<Note>()
                .HasGeneratedTsVectorColumn(n => n.SearchVector, "simple", n => new { n.Title, n.ContentMarkdown })
                .HasIndex(n => n.SearchVector)
                .HasMethod("GIN");
        }
        else
        {
            modelBuilder.Entity<Note>().Ignore(n => n.SearchVector);
        }

        modelBuilder.Entity<Tag>()
            .HasIndex(t => new { t.UserId, t.Name })
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
                    .HasIndex(r => r.Token)
                    .IsUnique();

        modelBuilder.Entity<RefreshToken>()
                    .HasOne(r => r.User)
                    .WithMany()
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
    }
}
