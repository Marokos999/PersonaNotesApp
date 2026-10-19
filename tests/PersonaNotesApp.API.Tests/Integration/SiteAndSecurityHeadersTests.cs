using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Tests.Integration;

public class SiteAndSecurityHeadersTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Responses_CarryTheHardeningHeaders()
    {
        var response = await _factory.CreateClient().GetAsync("/robots.txt");

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle("strict-origin-when-cross-origin");
        response.Headers.GetValues("Permissions-Policy").Should().ContainSingle().Which.Should().Contain("camera=()");
    }

    [Fact]
    public async Task ContentSecurityPolicy_LimitsScriptsToTheOwnOrigin_AndForbidsFraming()
    {
        var response = await _factory.CreateClient().GetAsync("/robots.txt");

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        csp.Should().Contain("script-src 'self' 'wasm-unsafe-eval'");
        csp.Should().NotContain("script-src 'self' 'unsafe-inline'");
        csp.Should().Contain("object-src 'none'").And.Contain("frame-ancestors 'none'").And.Contain("base-uri 'self'");
    }

    [Fact]
    public async Task HeadersAreAlsoSent_OnErrorResponses()
    {
        var response = await _factory.CreateClient().GetAsync("/api/notes/not-a-guid");

        response.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }

    [Fact]
    public async Task Hsts_IsSentOverHttps()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://pna.example.com") });

        var response = await client.GetAsync("/robots.txt");

        response.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle()
            .Which.Should().Contain("max-age=31536000");
    }

    [Fact]
    public async Task Hsts_IsNotSentOverPlainHttp()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://pna.example.com") });

        var response = await client.GetAsync("/robots.txt");

        response.Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task RobotsTxt_HidesTheApiAndPrivatePages_AndPointsToTheSitemap()
    {
        var response = await _factory.CreateClient().GetAsync("/robots.txt");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Disallow: /api/").And.Contain("Disallow: /notes").And.Contain("Disallow: /account");
        body.Should().Contain("Sitemap: http://localhost/sitemap.xml");
    }

    [Fact]
    public async Task Sitemap_ListsOnlyPublicPages_UsingTheConfiguredPublicUrl()
    {
        var client = WithSettings(("App:PublicUrl", "https://pna.example.com/")).CreateClient();

        var response = await client.GetAsync("/sitemap.xml");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<loc>https://pna.example.com/privacy</loc>")
            .And.Contain("<loc>https://pna.example.com/terms</loc>")
            .And.Contain("<loc>https://pna.example.com/login</loc>");
        body.Should().NotContain("/notes").And.NotContain("/account");
        (await client.GetStringAsync("/robots.txt")).Should().Contain("Sitemap: https://pna.example.com/sitemap.xml");
    }

    [Fact]
    public async Task AppInfo_ReportsTheConfiguredOperator_WithoutAuthentication()
    {
        var client = WithSettings(("Legal:OperatorName", "Jane Doe"), ("Legal:ContactEmail", "jane@example.com")).CreateClient();

        var info = await client.GetFromJsonAsync<AppInfoDto>("/api/app-info");

        info!.OperatorName.Should().Be("Jane Doe");
        info.ContactEmail.Should().Be("jane@example.com");
        info.LegalConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task AppInfo_IsNotConfigured_WhenNoOperatorIsSet()
    {
        var info = await _factory.CreateClient().GetFromJsonAsync<AppInfoDto>("/api/app-info");

        info!.Name.Should().Be("PersonaNotes");
        info.OperatorName.Should().BeNull();
        info.LegalConfigured.Should().BeFalse();
    }

    private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))));
}
