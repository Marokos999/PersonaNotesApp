using System.Net;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PersonaNotesApp.Shared.DTOs;
using PersonaNotesApp.Web.Pages;
using PersonaNotesApp.Web.Services;
using PersonaNotesApp.Web.Tests.Support;

namespace PersonaNotesApp.Web.Tests.Pages;

public class LegalPagesTests : BunitContext
{
    private readonly StubHttpHandler http = new();

    public LegalPagesTests()
    {
        Services.AddSingleton(new AppInfoService(http.CreateClient()));
    }

    private void Configure(string? operatorName, string? contactEmail) =>
        http.OnJson("GET", "/api/app-info", new AppInfoDto { OperatorName = operatorName, ContactEmail = contactEmail });

    [Fact]
    public void Privacy_ShowsTheConfiguredOperator_AndTheUsersRights()
    {
        Configure("Jane Doe", "jane@example.com");

        var cut = Render<Privacy>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Jane Doe"));
        cut.Find("a[href='mailto:jane@example.com']").TextContent.Should().Be("jane@example.com");
        cut.Markup.Should().Contain("Download my data").And.Contain("Delete account");
        cut.Find("a[href='/account']").Should().NotBeNull();
    }

    [Fact]
    public void Privacy_DisclosesGoogleFonts_AndTheAbsenceOfCookies()
    {
        Configure("Jane Doe", "jane@example.com");

        var cut = Render<Privacy>();

        cut.Markup.Should().Contain("Google Fonts").And.Contain("sets no cookies");
    }

    [Fact]
    public void Privacy_WarnsThatTheOperatorIsMissing_WhenNothingIsConfigured()
    {
        Configure(null, null);

        var cut = Render<Privacy>();

        cut.WaitForAssertion(() => cut.Find(".legal-notice").TextContent.Should().Contain("Legal__OperatorName"));
        cut.FindAll("a[href^='mailto:']").Should().BeEmpty();
    }

    [Fact]
    public void Terms_ShowsTheOperator_AndLinksToThePrivacyPolicy()
    {
        Configure("Jane Doe", "jane@example.com");

        var cut = Render<Terms>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Jane Doe"));
        cut.Find("a[href='/privacy']").Should().NotBeNull();
        cut.Find("a[href='mailto:jane@example.com']").Should().NotBeNull();
    }

    [Fact]
    public void Pages_StillRender_WhenTheServerCannotProvideTheOperatorDetails()
    {
        http.On("GET", "/api/app-info", HttpStatusCode.InternalServerError);

        var cut = Render<Terms>();

        cut.WaitForAssertion(() => cut.Find(".legal-notice").Should().NotBeNull());
        cut.Markup.Should().Contain("Terms of Service");
    }
}
