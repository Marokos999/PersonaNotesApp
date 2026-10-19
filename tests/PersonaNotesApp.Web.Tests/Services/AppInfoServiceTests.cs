using System.Net;
using FluentAssertions;
using PersonaNotesApp.Shared.DTOs;
using PersonaNotesApp.Web.Services;
using PersonaNotesApp.Web.Tests.Support;

namespace PersonaNotesApp.Web.Tests.Services;

public class AppInfoServiceTests
{
    private readonly StubHttpHandler http = new();

    [Fact]
    public async Task LoadsTheDetailsOnce_AndReusesThem()
    {
        http.OnJson("GET", "/api/app-info", new AppInfoDto { OperatorName = "Jane Doe", ContactEmail = "jane@example.com" });
        var service = new AppInfoService(http.CreateClient());

        var first = await service.GetAsync();
        var second = await service.GetAsync();

        first.OperatorName.Should().Be("Jane Doe");
        second.Should().BeSameAs(first);
        http.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ReturnsAnUnconfiguredInstance_WhenTheRequestFails_AndRetriesNextTime()
    {
        http.On("GET", "/api/app-info", HttpStatusCode.InternalServerError);
        var service = new AppInfoService(http.CreateClient());

        var failed = await service.GetAsync();

        failed.LegalConfigured.Should().BeFalse();

        http.OnJson("GET", "/api/app-info", new AppInfoDto { OperatorName = "Jane Doe", ContactEmail = "jane@example.com" });
        (await service.GetAsync()).LegalConfigured.Should().BeTrue();
    }
}
