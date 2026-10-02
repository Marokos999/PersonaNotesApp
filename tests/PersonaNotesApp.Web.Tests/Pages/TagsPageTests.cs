using System.Net;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PersonaNotesApp.Shared.DTOs;
using PersonaNotesApp.Web.Pages;
using PersonaNotesApp.Web.Services;
using PersonaNotesApp.Web.Tests.Support;

namespace PersonaNotesApp.Web.Tests.Pages;

public class TagsPageTests : BunitContext
{
    private readonly StubHttpHandler http = new();

    public TagsPageTests()
    {
        Services.AddSingleton(new TagService(http.CreateClient()));
    }

    private static List<TagDto> TwoTags() =>
    [
        new() { Id = Guid.NewGuid(), Name = "Work", Color = "#3B82F6", NoteCount = 2 },
        new() { Id = Guid.NewGuid(), Name = "Home", Color = "#10B981", NoteCount = 1 }
    ];

    [Fact]
    public void ShowsOneRowPerTag_WithNoteCounts()
    {
        http.OnJson("GET", "/api/tags", TwoTags());

        var cut = Render<Tags>();

        cut.WaitForAssertion(() => cut.FindAll(".tag-row").Count.Should().Be(3)); // 2 tags + "new tag" row
        cut.Markup.Should().Contain("2 notes").And.Contain("1 note");
    }

    [Fact]
    public void ShowsEmptyState_WhenThereAreNoTags()
    {
        http.OnJson("GET", "/api/tags", new List<TagDto>());

        var cut = Render<Tags>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No tags yet."));
    }

    [Fact]
    public void Add_PostsTheNewTag_AndReloadsTheList()
    {
        var tags = new List<TagDto>();
        http.On("GET", "/api/tags", _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(tags) });
        http.On("POST", "/api/tags", _ =>
        {
            tags.Add(new TagDto { Id = Guid.NewGuid(), Name = "Ideas", Color = "#6B7280" });
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = System.Net.Http.Json.JsonContent.Create(tags[0]) };
        });
        var cut = Render<Tags>();
        cut.WaitForState(() => cut.Markup.Contains("No tags yet."));

        cut.Find("input[placeholder='New tag name']").Input("Ideas");
        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("No tags yet."));
        http.Requests.Single(r => r.Method == "POST").Body.Should().Contain("\"name\":\"Ideas\"");
        cut.FindAll("input.form-control").Select(i => i.GetAttribute("value")).Should().Contain("Ideas");
    }

    [Fact]
    public void Save_ShowsError_WhenServerRejectsTheChange()
    {
        var tags = TwoTags();
        http.OnJson("GET", "/api/tags", tags);
        http.On("PUT", $"/api/tags/{tags[0].Id}", HttpStatusCode.Conflict);
        var cut = Render<Tags>();
        cut.WaitForState(() => cut.FindAll(".tag-row").Count == 3);

        cut.FindAll("button.btn-outline-primary")[0].Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Names must be unique"));
    }

    [Fact]
    public void Delete_CallsApi_OnlyAfterConfirmation()
    {
        var tags = TwoTags();
        http.OnJson("GET", "/api/tags", tags);
        http.On("DELETE", $"/api/tags/{tags[0].Id}", HttpStatusCode.NoContent);
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);
        var cut = Render<Tags>();
        cut.WaitForState(() => cut.FindAll(".tag-row").Count == 3);

        cut.FindAll("button.btn-outline-danger")[0].Click();
        cut.WaitForAssertion(() => JSInterop.Invocations["confirm"].Should().HaveCount(1));
        http.Requests.Should().NotContain(r => r.Method == "DELETE");

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        cut.FindAll("button.btn-outline-danger")[0].Click();

        cut.WaitForAssertion(() => http.Requests.Should().Contain(r => r.Method == "DELETE" && r.Path == $"/api/tags/{tags[0].Id}"));
    }
}
