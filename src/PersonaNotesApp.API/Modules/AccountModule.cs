using System.Security.Claims;
using System.Text.Json;
using Carter;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonaNotesApp.API.Application.Extensions;
using PersonaNotesApp.API.Application.Services;
using PersonaNotesApp.API.Application.Validation;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Modules;

public class AccountModule : ICarterModule
{
    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var accountGroup = app.MapGroup("/api/account").RequireAuthorization();

        // GET /api/account/export: downloads everything stored about the user as a JSON file.
        accountGroup.MapGet("/export", async (UserManager<ApplicationUser> users, AccountService accounts, ClaimsPrincipal principal) =>
        {
            var user = await users.FindByIdAsync(principal.GetUserId());
            if (user is null) return Results.NotFound();

            var export = await accounts.ExportAsync(user);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(export, ExportJson);
            return Results.File(bytes, "application/json", $"personanotes-export-{export.ExportedAt:yyyy-MM-dd}.json");
        });

        // DELETE /api/account: permanently erases the account and all its data after confirming the password.
        // Shares the auth rate limit, because this endpoint is also a password oracle for a stolen access token.
        // A wrong password is a 400, never a 401: the client would treat a 401 as an expired session and sign out.
        accountGroup.MapDelete("/", async ([FromBody] DeleteAccountRequest req, UserManager<ApplicationUser> users,
                AccountService accounts, ClaimsPrincipal principal) =>
            {
                var user = await users.FindByIdAsync(principal.GetUserId());
                if (user is null) return Results.NotFound();

                if (!await users.CheckPasswordAsync(user, req.Password))
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Incorrect password.");

                var result = await accounts.DeleteAsync(user);
                return result.Succeeded
                    ? Results.NoContent()
                    : Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "The account could not be deleted.");
            })
            .WithValidation<DeleteAccountRequest>()
            .RequireRateLimiting(AuthModule.RateLimitPolicy);
    }
}
