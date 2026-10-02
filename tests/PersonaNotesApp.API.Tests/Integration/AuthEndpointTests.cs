using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Tests.Integration;

public class AuthEndpointTests : IDisposable
{
    private const string ValidPassword = "Passw0rd!";

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public AuthEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Register_ReturnsTokens_WhenRequestIsValid()
    {
        var response = await Register("new@example.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.Should().NotBeNullOrEmpty();
        auth.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("not-an-email", ValidPassword)]
    [InlineData("", ValidPassword)]
    [InlineData("user@example.com", "short")]
    [InlineData("user@example.com", "alllowercase1!")]
    public async Task Register_ReturnsBadRequest_WhenInputIsInvalid(string email, string password)
    {
        var response = await Register(email, password);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenEmailAlreadyRegistered()
    {
        await Register("dup@example.com");

        var response = await Register("dup@example.com");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_ReturnsTokens_WithCorrectCredentials()
    {
        await Register("login@example.com");

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "login@example.com", Password = ValidPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WithWrongPassword()
    {
        await Register("wrong@example.com");

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "wrong@example.com", Password = "Wr0ng-password!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForUnknownUser()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "nobody@example.com", Password = ValidPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ReturnsNewTokens_AndRevokesTheOldRefreshToken()
    {
        var first = await RegisterAndGetTokens("refresh@example.com");

        var refreshed = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = first.RefreshToken });

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = (await refreshed.Content.ReadFromJsonAsync<AuthResponse>())!;
        second.RefreshToken.Should().NotBe(first.RefreshToken);

        var replay = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = first.RefreshToken });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_ForUnknownToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = "does-not-exist" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var tokens = await RegisterAndGetTokens("logout@example.com");

        var logout = await _client.PostAsJsonAsync("/api/auth/logout",
            new RefreshRequest { RefreshToken = tokens.RefreshToken });
        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = tokens.RefreshToken });

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> Register(string email, string password = ValidPassword) =>
        _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = password });

    private async Task<AuthResponse> RegisterAndGetTokens(string email)
    {
        var response = await Register(email);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}
