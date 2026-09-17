using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.API.Infrastructure;

namespace PersonaNotesApp.API.Application.Services;

public class TokenService(IConfiguration config, AppDbContext db)
{
    public async Task<(string Access, string Refresh)> GenerateAsync(ApplicationUser user)
    {
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
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(config.GetValue<int>("Jwt:RefreshTokenExpiryDays"))
        });

        await db.SaveChangesAsync();

        return (accessToken, refreshToken);
    }
}
