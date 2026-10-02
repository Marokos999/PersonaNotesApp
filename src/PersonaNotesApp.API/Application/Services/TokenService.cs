using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.API.Infrastructure;

namespace PersonaNotesApp.API.Application.Services;

public class TokenService(IConfiguration config, AppDbContext db)
{
    /// <summary>Refresh tokens are stored only as SHA-256 hashes; the plain value exists only in the response.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<(string Access, string Refresh)> GenerateAsync(ApplicationUser user)
    {
        var tokens = await CreateAsync(user);
        await db.SaveChangesAsync();
        return tokens;
    }

    /// <summary>Exchanges a valid refresh token for a new pair; the old token is revoked. Null if invalid or expired.</summary>
    public async Task<(string Access, string Refresh)?> RotateAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == hash && !r.IsRevoked);

        if (stored is null || stored.ExpiresAt < DateTime.UtcNow)
            return null;

        var tokens = await CreateAsync(stored.User);
        stored.IsRevoked = true;
        await db.SaveChangesAsync();
        return tokens;
    }

    public async Task RevokeAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == hash && !r.IsRevoked);
        if (stored is null) return;

        stored.IsRevoked = true;
        await db.SaveChangesAsync();
    }

    private async Task<(string Access, string Refresh)> CreateAsync(ApplicationUser user)
    {
        await PurgeStaleTokensAsync(user.Id);

        var claims = new []
        {
         new Claim(ClaimTypes.NameIdentifier, user.Id),
         new Claim(ClaimTypes.Email, user.Email!)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));

        var jwt = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(config.GetValue<int>("Jwt:AccessTokenExpiryMinutes")),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = Hash(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(config.GetValue<int>("Jwt:RefreshTokenExpiryDays"))
        });

        return (accessToken, refreshToken);
    }

    private async Task PurgeStaleTokensAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var stale = await db.RefreshTokens
            .Where(r => r.UserId == userId && (r.IsRevoked || r.ExpiresAt < now))
            .ToListAsync();
        db.RefreshTokens.RemoveRange(stale);
    }
}
