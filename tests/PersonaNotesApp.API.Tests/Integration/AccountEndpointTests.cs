using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PersonaNotesApp.API.Application.Services;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.API.Infrastructure;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Tests.Integration;

public class AccountEndpointTests : IDisposable
{
    private const string Password = "Passw0rd!";

    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Export_ReturnsTheUsersNotesAndTags_AsAJsonDownload()
    {
        var alice = await CreateUser("alice@example.com");
        var client = _factory.CreateClientAs(alice.Id);
        await CreateNote(client, "First", ["Work", "Home"]);
        var pinned = await CreateNote(client, "Second", ["Work"]);
        (await client.PatchAsync($"/api/notes/{pinned.Id}/pin", null)).EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/account/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentDisposition!.FileName.Should().StartWith("personanotes-export-").And.EndWith(".json");
        var export = await response.Content.ReadFromJsonAsync<AccountExportDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        export!.Email.Should().Be("alice@example.com");
        export.Tags.Select(t => t.Name).Should().Equal("Home", "Work");
        export.Notes.Select(n => n.Title).Should().Equal("First", "Second");
        export.Notes[0].Tags.Should().Equal("Home", "Work");
        export.Notes[0].Content.Should().Be("content of First");
        export.Notes[1].IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task Export_NeverIncludesAnotherUsersData()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        await CreateNote(_factory.CreateClientAs(alice.Id), "Alice secret", ["Private"]);
        await CreateNote(_factory.CreateClientAs(bob.Id), "Bob note", []);

        var export = await _factory.CreateClientAs(bob.Id).GetFromJsonAsync<AccountExportDto>("/api/account/export",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        export!.Notes.Should().ContainSingle().Which.Title.Should().Be("Bob note");
        export.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Export_ReturnsNotFound_WhenTheAccountNoLongerExists()
    {
        var response = await _factory.CreateClientAs("ghost-user").GetAsync("/api/account/export");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ErasesTheAccountAndAllItsData_AndLeavesOtherUsersAlone()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        var aliceClient = _factory.CreateClientAs(alice.Id);
        await CreateNote(aliceClient, "Alice note", ["Work"]);
        await CreateNote(_factory.CreateClientAs(bob.Id), "Bob note", ["Work"]);
        await IssueRefreshToken(alice);

        var response = await Delete(aliceClient, Password);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.AnyAsync(u => u.Id == alice.Id)).Should().BeFalse();
        (await db.Notes.AnyAsync(n => n.UserId == alice.Id)).Should().BeFalse();
        (await db.Tags.AnyAsync(t => t.UserId == alice.Id)).Should().BeFalse();
        (await db.RefreshTokens.AnyAsync(r => r.UserId == alice.Id)).Should().BeFalse();
        (await db.NoteTags.CountAsync()).Should().Be(1, "only Bob's note-tag link remains");
        (await db.Users.AnyAsync(u => u.Id == bob.Id)).Should().BeTrue();
        (await db.Notes.CountAsync(n => n.UserId == bob.Id)).Should().Be(1);
        (await db.Tags.CountAsync(t => t.UserId == bob.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_AndKeepsEverything_WhenThePasswordIsWrong()
    {
        var alice = await CreateUser("alice@example.com");
        var client = _factory.CreateClientAs(alice.Id);
        await CreateNote(client, "Alice note", []);

        var response = await Delete(client, "Wr0ng-password!");

        // Must not be a 401: the Blazor client would treat that as an expired session and sign the user out.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<List<NoteDto>>("/api/notes")).Should().ContainSingle();
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(alice.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenThePasswordIsMissing()
    {
        var alice = await CreateUser("alice@example.com");

        var response = await _factory.CreateClientAs(alice.Id).SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/account") { Content = JsonContent.Create(new { }) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenTheAccountNoLongerExists()
    {
        var response = await Delete(_factory.CreateClientAs("ghost-user"), Password);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletedUser_CanNoLongerLogIn_AndTheEmailCanBeRegisteredAgain()
    {
        var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/auth/register", new { Email = "gone@example.com", Password })).EnsureSuccessStatusCode();
        string userId;
        using (var scope = _factory.Services.CreateScope())
            userId = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("gone@example.com"))!.Id;

        (await Delete(_factory.CreateClientAs(userId), Password)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await anonymous.PostAsJsonAsync("/api/auth/login", new { Email = "gone@example.com", Password })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/auth/register", new { Email = "gone@example.com", Password })).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> Delete(HttpClient client, string password) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest { Password = password })
        });

    private static async Task<NoteDto> CreateNote(HttpClient client, string title, List<string> tags)
    {
        var response = await client.PostAsJsonAsync("/api/notes",
            new CreateNoteRequest { Title = title, Content = $"content of {title}", Tags = tags });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NoteDto>())!;
    }

    private async Task<ApplicationUser> CreateUser(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var user = new ApplicationUser { UserName = email, Email = email };
        (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, Password))
            .Succeeded.Should().BeTrue();
        return user;
    }

    private async Task IssueRefreshToken(ApplicationUser user)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TokenService>().GenerateAsync(user);
    }
}
