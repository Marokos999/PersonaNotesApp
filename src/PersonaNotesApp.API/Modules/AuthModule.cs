using Carter;
using Microsoft.AspNetCore.Identity;
using PersonaNotesApp.API.Application.Services;
using PersonaNotesApp.API.Application.Validation;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Modules;

public class AuthModule : ICarterModule
{
    public const string RateLimitPolicy = "auth";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/api/auth").RequireRateLimiting(RateLimitPolicy);

        // POST /api/auth/register
        authGroup.MapPost("/register", async (RegisterRequest req, UserManager<ApplicationUser> users, TokenService tokens) =>
        {
            if (await users.FindByEmailAsync(req.Email) is not null)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Email is already registered.");

            var user = new ApplicationUser
            {
              UserName = req.Email,
              Email = req.Email
            };
            var result = await users.CreateAsync(user, req.Password);
            if (!result.Succeeded)
            {
                if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
                    return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Email is already registered.");

                var errors = result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return Results.ValidationProblem(errors);
            }

            var (access, refresh) = await tokens.GenerateAsync(user);

            return Results.Ok(new AuthResponse
            {
                AccessToken = access,
                RefreshToken = refresh
            });
        })
        .WithValidation<RegisterRequest>();

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
        })
        .WithValidation<LoginRequest>();

        // POST /api/auth/refresh
        authGroup.MapPost("/refresh", async (RefreshRequest req, TokenService tokens) =>
        {
            var result = await tokens.RotateAsync(req.RefreshToken);
            if (result is null)
                return Results.Unauthorized();

            return Results.Ok(new AuthResponse
            {
                AccessToken = result.Value.Access,
                RefreshToken = result.Value.Refresh
            });
        })
        .WithValidation<RefreshRequest>();

        // POST /api/auth/logout
        authGroup.MapPost("/logout", async (RefreshRequest req, TokenService tokens) =>
        {
            await tokens.RevokeAsync(req.RefreshToken);
            return Results.NoContent();
        })
        .WithValidation<RefreshRequest>();
    }
}
