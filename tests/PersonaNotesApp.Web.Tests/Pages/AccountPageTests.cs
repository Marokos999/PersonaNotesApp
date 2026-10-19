using System.Net;
using System.Text;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PersonaNotesApp.Web.Auth;
using PersonaNotesApp.Web.Pages;
using PersonaNotesApp.Web.Services;
using PersonaNotesApp.Web.Tests.Support;

namespace PersonaNotesApp.Web.Tests.Pages;

public class AccountPageTests : BunitContext
{
    private const string Password = "Passw0rd!";

    private readonly StubHttpHandler http = new();

    public AccountPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = new LocalStorageService(JSInterop.JSRuntime);
        var refresher = new TokenRefresher(storage, new NoClientFactory());
        Services.AddSingleton(new AccountService(http.CreateClient()));
        Services.AddSingleton(new JwtAuthProvider(storage, refresher));
    }

    private IRenderedComponent<Account> RenderPage() => Render<Account>();

    private static void FillForm(IRenderedComponent<Account> cut, string password, bool confirm)
    {
        cut.Find("#confirm-password").Input(password);
        if (confirm) cut.Find(".account-confirm input").Change(true);
    }

    [Fact]
    public void DeleteButton_StaysDisabled_UntilThePasswordAndTheConfirmationAreGiven()
    {
        var cut = RenderPage();
        cut.Find("button.btn-danger").HasAttribute("disabled").Should().BeTrue();

        FillForm(cut, Password, confirm: false);
        cut.Find("button.btn-danger").HasAttribute("disabled").Should().BeTrue();

        cut.Find(".account-confirm input").Change(true);
        cut.Find("button.btn-danger").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Delete_SendsThePassword_ThenSignsOutAndGoesToTheLoginPage()
    {
        http.On("DELETE", "/api/account", HttpStatusCode.NoContent);
        var cut = RenderPage();
        FillForm(cut, Password, confirm: true);

        cut.Find("button.btn-danger").Click();

        cut.WaitForAssertion(() => Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/login?deleted=true"));
        http.Requests.Single(r => r.Method == "DELETE").Body.Should().Contain($"\"password\":\"{Password}\"");
        JSInterop.Invocations.Should().Contain(i => i.Identifier == "localStorage.removeItem" && (string?)i.Arguments[0] == JwtAuthProvider.RefreshTokenKey);
    }

    [Fact]
    public void Delete_ShowsAnError_AndStaysOnThePage_WhenThePasswordIsWrong()
    {
        http.On("DELETE", "/api/account", HttpStatusCode.BadRequest);
        var cut = RenderPage();
        FillForm(cut, "wrong", confirm: true);

        cut.Find("button.btn-danger").Click();

        cut.WaitForAssertion(() => cut.Find(".auth-error").TextContent.Should().Be("Incorrect password."));
        Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("/login");
        JSInterop.Invocations.Should().NotContain(i => i.Identifier == "localStorage.removeItem");
    }

    [Fact]
    public void Delete_ExplainsTheRateLimit()
    {
        http.On("DELETE", "/api/account", HttpStatusCode.TooManyRequests);
        var cut = RenderPage();
        FillForm(cut, Password, confirm: true);

        cut.Find("button.btn-danger").Click();

        cut.WaitForAssertion(() => cut.Find(".auth-error").TextContent.Should().Contain("Too many attempts"));
    }

    [Fact]
    public void Export_HandsTheDownloadedFileToTheBrowser_UnderTheNameFromTheServer()
    {
        http.On("GET", "/api/account/export", _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("{}")) };
            response.Content.Headers.ContentDisposition = new("attachment") { FileName = "personanotes-export-2026-10-19.json" };
            return response;
        });
        JSInterop.SetupVoid("fileInterop.download", _ => true);
        var cut = RenderPage();

        cut.Find("button.btn-outline-primary").Click();

        cut.WaitForAssertion(() => JSInterop.Invocations["fileInterop.download"].Single()
            .Arguments[0].Should().Be("personanotes-export-2026-10-19.json"));
    }

    [Fact]
    public void Export_ShowsAnError_WhenTheServerRefuses()
    {
        http.On("GET", "/api/account/export", HttpStatusCode.InternalServerError);
        var cut = RenderPage();

        cut.Find("button.btn-outline-primary").Click();

        cut.WaitForAssertion(() => cut.Find(".auth-error").TextContent.Should().Contain("could not be downloaded"));
        JSInterop.Invocations.Should().NotContain(i => i.Identifier == "fileInterop.download");
    }

    private sealed class NoClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("No token refresh is expected in these tests.");
    }
}
