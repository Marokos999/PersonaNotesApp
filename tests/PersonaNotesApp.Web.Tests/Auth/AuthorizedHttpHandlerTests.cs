using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PersonaNotesApp.Shared.DTOs;
using PersonaNotesApp.Web.Auth;
using PersonaNotesApp.Web.Tests.Support;

namespace PersonaNotesApp.Web.Tests.Auth;

public class AuthorizedHttpHandlerTests
{
    private readonly FakeJsRuntime js = new();
    private readonly StubHttpHandler api = new();
    private readonly StubHttpHandler authApi = new();
    private readonly HttpClient client;
    private readonly LocalStorageService storage;

    public AuthorizedHttpHandlerTests()
    {
        storage = new LocalStorageService(js);
        var refresher = new TokenRefresher(storage, new SingleClientFactory(authApi.CreateClient()));
        var auth = new JwtAuthProvider(storage, refresher);
        client = new HttpClient(new AuthorizedHttpHandler(storage, refresher, auth) { InnerHandler = api })
        {
            BaseAddress = new Uri("http://localhost/")
        };
    }

    [Fact]
    public async Task AddsBearerToken_WhenLoggedIn()
    {
        js.Storage[JwtAuthProvider.AccessTokenKey] = "access-1";
        api.On("GET", "/api/notes", HttpStatusCode.OK);

        await client.GetAsync("api/notes");

        api.Requests.Single().Authorization.Should().Be("Bearer access-1");
    }

    [Fact]
    public async Task On401_RefreshesTokensAndReplaysRequestWithBody()
    {
        js.Storage[JwtAuthProvider.AccessTokenKey] = "expired";
        js.Storage[JwtAuthProvider.RefreshTokenKey] = "refresh-1";
        api.On("POST", "/api/notes", req =>
            new HttpResponseMessage(req.Headers.Authorization?.Parameter == "access-2"
                ? HttpStatusCode.Created
                : HttpStatusCode.Unauthorized));
        authApi.OnJson("POST", "/api/auth/refresh", new AuthResponse { AccessToken = "access-2", RefreshToken = "refresh-2" });

        var response = await client.PostAsJsonAsync("api/notes", new { title = "hello" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        api.Requests.Should().HaveCount(2);
        api.Requests[1].Authorization.Should().Be("Bearer access-2");
        api.Requests[1].Body.Should().Be(api.Requests[0].Body).And.Contain("hello");
        authApi.Requests.Single().Body.Should().Contain("refresh-1");
        js.Storage[JwtAuthProvider.AccessTokenKey].Should().Be("access-2");
        js.Storage[JwtAuthProvider.RefreshTokenKey].Should().Be("refresh-2");
    }

    [Fact]
    public async Task On401_WhenRefreshFails_ReturnsUnauthorizedAndClearsSession()
    {
        js.Storage[JwtAuthProvider.AccessTokenKey] = "expired";
        js.Storage[JwtAuthProvider.RefreshTokenKey] = "revoked";
        api.On("GET", "/api/notes", HttpStatusCode.Unauthorized);
        authApi.On("POST", "/api/auth/refresh", HttpStatusCode.Unauthorized);

        var response = await client.GetAsync("api/notes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        api.Requests.Should().HaveCount(1, "a failed refresh must not replay the request");
        js.Storage.Should().BeEmpty();
    }

    [Fact]
    public async Task AuthEndpoints_AreNeverRefreshedOrReplayed()
    {
        js.Storage[JwtAuthProvider.RefreshTokenKey] = "refresh-1";
        api.On("POST", "/api/auth/login", HttpStatusCode.Unauthorized);

        var response = await client.PostAsJsonAsync("api/auth/login", new { email = "a", password = "b" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        api.Requests.Should().HaveCount(1);
        authApi.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentUnauthorizedRequests_ShareASingleRefresh()
    {
        js.Storage[JwtAuthProvider.AccessTokenKey] = "expired";
        js.Storage[JwtAuthProvider.RefreshTokenKey] = "refresh-1";
        api.On("GET", "/api/notes", req =>
            new HttpResponseMessage(req.Headers.Authorization?.Parameter == "access-2"
                ? HttpStatusCode.OK
                : HttpStatusCode.Unauthorized));
        authApi.OnJson("POST", "/api/auth/refresh", new AuthResponse { AccessToken = "access-2", RefreshToken = "refresh-2" });

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.GetAsync("api/notes")));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        authApi.Requests.Should().ContainSingle("the refresh token is single-use");
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
