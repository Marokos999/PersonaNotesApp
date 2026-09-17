using Carter;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Application.Services;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.API.Infrastructure;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Modules;

public class AuthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/api/auth");

        // POST /api/auth/register
        authGroup.MapPost("/register", async (RegisterRequest req, UserManager<ApplicationUser> users, TokenService tokens) =>
        {
            var user = new ApplicationUser
            {
              UserName = req.Email,
              Email = req.Email
            };
            var result = await users.CreateAsync(user, req.Password);
            if(!result.Succeeded)
                return Results.BadRequest(result.Errors.Select(e => e.Description));

            var (access, refresh) = await tokens.GenerateAsync(user);

            return Results.Ok(new AuthResponse
            {
                AccessToken = access,
                RefreshToken = refresh
            });
        });
        // POST /api/auth/login
        authGroup.MapPost("/login", async (LoginRequest req, UserManager<ApplicationUser> users, TokenService tokens) =>
        {
            var user = await users.FindByEmailAsync(req.Email);
            if(user is null || !await users.CheckPasswordAsync(user, req.Password))
                return Results.Unauthorized();

            var (access, refresh) = await tokens.GenerateAsync(user);

            return Results.Ok(new AuthResponse
            {
                AccessToken  = access,
                RefreshToken = refresh
            });
        });
        // POST /api/auth/refresh
        authGroup.MapPost("/refresh", async(RefreshRequest req, AppDbContext db, TokenService tokens) =>
        {
            var stored = await db.RefreshTokens
                                 .Include(r => r.User)
                                 .FirstOrDefaultAsync(r => r.Token == req.RefreshToken && !r.IsRevoked);

            if(stored is null || stored.ExpiresAt < DateTime.UtcNow)
                return Results.Unauthorized();

            stored.IsRevoked = true;
            var (access, refresh) = await tokens.GenerateAsync(stored.User);
            await db.SaveChangesAsync();
            return Results.Ok(new AuthResponse
            {
                AccessToken = access,
                RefreshToken = refresh
            });
        });
        // POST /api/auth/logout
        authGroup.MapPost("/logout", async (RefreshRequest req, AppDbContext db) =>
        {
            var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == req.RefreshToken && !r.IsRevoked);

            if(stored is not null)
            {
                stored.IsRevoked = true;
                await db.SaveChangesAsync();
            }

            return Results.NoContent();
        });
    }
}
