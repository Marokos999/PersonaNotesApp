using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PersonaNotesApp.API.Application.Services;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.API.Infrastructure;

namespace PersonaNotesApp.API.Tests.Unit.Services;

public class TokenServiceTests : IDisposable
{
    private readonly AppDbContext db;
    private readonly TokenService sut;
    private readonly ApplicationUser user = new() { Id = "u1", Email = "u1@example.com", UserName = "u1@example.com" };

    public TokenServiceTests()
    {
        db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Users.Add(user);
        db.SaveChanges();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "unit-test-secret-key-0123456789-abcdefghij",
            ["Jwt:Issuer"] = "tests",
            ["Jwt:Audience"] = "tests",
            ["Jwt:AccessTokenExpiryMinutes"] = "15",
            ["Jwt:RefreshTokenExpiryDays"] = "30"
        }).Build();

        sut = new TokenService(config, db);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task GenerateAsync_StoresOnlyTheHashOfTheRefreshToken()
    {
        var (_, refresh) = await sut.GenerateAsync(user);

        var stored = await db.RefreshTokens.SingleAsync();
        stored.Token.Should().NotBe(refresh);
        stored.Token.Should().Be(TokenService.Hash(refresh));
        stored.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task RotateAsync_ReturnsNewPair_AndRevokesTheOldToken()
    {
        var (_, refresh) = await sut.GenerateAsync(user);

        var rotated = await sut.RotateAsync(refresh);

        rotated.Should().NotBeNull();
        rotated!.Value.Refresh.Should().NotBe(refresh);
        (await sut.RotateAsync(refresh)).Should().BeNull("a refresh token can only be used once");
    }

    [Fact]
    public async Task RotateAsync_ReturnsNull_ForUnknownToken()
    {
        (await sut.RotateAsync("unknown")).Should().BeNull();
    }

    [Fact]
    public async Task RotateAsync_ReturnsNull_ForExpiredToken()
    {
        var (_, refresh) = await sut.GenerateAsync(user);
        var stored = await db.RefreshTokens.SingleAsync();
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        (await sut.RotateAsync(refresh)).Should().BeNull();
    }

    [Fact]
    public async Task RevokeAsync_MakesTokenUnusable()
    {
        var (_, refresh) = await sut.GenerateAsync(user);

        await sut.RevokeAsync(refresh);

        (await sut.RotateAsync(refresh)).Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_PurgesExpiredAndRevokedTokensOfTheUser()
    {
        db.RefreshTokens.AddRange(
            new RefreshToken { UserId = user.Id, Token = "expired", ExpiresAt = DateTime.UtcNow.AddDays(-1) },
            new RefreshToken { UserId = user.Id, Token = "revoked", ExpiresAt = DateTime.UtcNow.AddDays(5), IsRevoked = true },
            new RefreshToken { UserId = user.Id, Token = "active", ExpiresAt = DateTime.UtcNow.AddDays(5) });
        await db.SaveChangesAsync();

        await sut.GenerateAsync(user);

        var tokens = await db.RefreshTokens.Select(t => t.Token).ToListAsync();
        tokens.Should().NotContain(["expired", "revoked"]);
        tokens.Should().Contain("active");
        tokens.Should().HaveCount(2);
    }
}
