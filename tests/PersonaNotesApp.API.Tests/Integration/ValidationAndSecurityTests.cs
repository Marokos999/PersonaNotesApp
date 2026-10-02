using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Tests.Integration;

public class ValidationAndSecurityTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public ValidationAndSecurityTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task CreateNote_StripsScriptsAndEventHandlers_FromRenderedHtml()
    {
        var markdown = "hello <script>alert(1)</script> <img src=x onerror=alert(2)> [x](javascript:alert(3))";

        var response = await _client.PostAsJsonAsync("/api/notes",
            new CreateNoteRequest { Title = "xss", Content = markdown });

        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        note!.ContentHtml.Should().NotContainEquivalentOf("<script");
        note.ContentHtml.Should().NotContainEquivalentOf("onerror");
        note.ContentHtml.Should().NotContainEquivalentOf("javascript:");
        note.Content.Should().Be(markdown, "the raw markdown is preserved for editing");
    }

    [Fact]
    public async Task CreateNote_KeepsTaskListCheckboxes()
    {
        var response = await _client.PostAsJsonAsync("/api/notes",
            new CreateNoteRequest { Title = "todo", Content = "- [ ] one\n- [x] two" });

        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        note!.ContentHtml.Should().Contain("type=\"checkbox\"");
        note.ContentHtml.Should().Contain("checked");
    }

    [Fact]
    public async Task CreateNote_RemovesNonCheckboxInputs()
    {
        var response = await _client.PostAsJsonAsync("/api/notes",
            new CreateNoteRequest { Title = "form", Content = "<input type=\"text\" name=\"password\">" });

        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        note!.ContentHtml.Should().NotContain("<input");
    }

    [Fact]
    public async Task CreateNote_ReturnsBadRequest_WhenTitleIsEmpty()
    {
        var response = await _client.PostAsJsonAsync("/api/notes",
            new CreateNoteRequest { Title = "", Content = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateNote_ReturnsBadRequest_WhenTitleIsTooLong()
    {
        var response = await _client.PostAsJsonAsync("/api/notes",
            new CreateNoteRequest { Title = new string('a', NoteWriteRequest.MaxTitleLength + 1), Content = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateNote_ReturnsBadRequest_WhenTagNameIsTooLong()
    {
        var response = await _client.PostAsJsonAsync("/api/notes",
            new CreateNoteRequest { Title = "t", Content = "x", Tags = [new string('a', TagDto.MaxNameLength + 1)] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateNote_DeduplicatesAndTrimsTags()
    {
        var response = await _client.PostAsJsonAsync("/api/notes",
            new CreateNoteRequest { Title = "t", Content = "x", Tags = ["Work", " Work ", "", "Ideas"] });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        note!.Tags.Select(t => t.Name).Should().BeEquivalentTo("Work", "Ideas");
    }

    [Theory]
    [InlineData("", "#112233")]
    [InlineData("Work", "blue")]
    [InlineData("Work", "#12345")]
    public async Task CreateTag_ReturnsBadRequest_WhenInputIsInvalid(string name, string color)
    {
        var response = await _client.PostAsJsonAsync("/api/tags", new TagDto { Name = name, Color = color });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTag_ReturnsConflict_WhenNameAlreadyExists()
    {
        await _client.PostAsJsonAsync("/api/tags", new TagDto { Name = "Work", Color = "#112233" });

        var response = await _client.PostAsJsonAsync("/api/tags", new TagDto { Name = "Work", Color = "#445566" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateTag_ChangesNameAndColor_AndRejectsDuplicateName()
    {
        var work = await (await _client.PostAsJsonAsync("/api/tags", new TagDto { Name = "Work", Color = "#112233" }))
            .Content.ReadFromJsonAsync<TagDto>();
        await _client.PostAsJsonAsync("/api/tags", new TagDto { Name = "Home", Color = "#445566" });

        var ok = await _client.PutAsJsonAsync($"/api/tags/{work!.Id}", new TagDto { Name = "Office", Color = "#ABCDEF" });
        var conflict = await _client.PutAsJsonAsync($"/api/tags/{work.Id}", new TagDto { Name = "Home", Color = "#ABCDEF" });

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ok.Content.ReadFromJsonAsync<TagDto>();
        updated!.Name.Should().Be("Office");
        updated.Color.Should().Be("#ABCDEF");
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteTag_RemovesTag_AndDetachesItFromNotes()
    {
        var note = await (await _client.PostAsJsonAsync("/api/notes",
            new CreateNoteRequest { Title = "t", Content = "x", Tags = ["Work"] })).Content.ReadFromJsonAsync<NoteDto>();

        var delete = await _client.DeleteAsync($"/api/tags/{note!.Tags[0].Id}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetFromJsonAsync<List<TagDto>>("/api/tags")).Should().BeEmpty();
        (await _client.GetFromJsonAsync<NoteDto>($"/api/notes/{note.Id}"))!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTags_ReportsNoteCount()
    {
        await _client.PostAsJsonAsync("/api/notes", new CreateNoteRequest { Title = "a", Content = "x", Tags = ["Work"] });
        await _client.PostAsJsonAsync("/api/notes", new CreateNoteRequest { Title = "b", Content = "x", Tags = ["Work"] });

        var tags = await _client.GetFromJsonAsync<List<TagDto>>("/api/tags");

        tags.Should().ContainSingle().Which.NoteCount.Should().Be(2);
    }

    [Fact]
    public async Task GetNotes_PaginatesAndExposesTotalCountHeader()
    {
        for (var i = 0; i < 5; i++)
            await _client.PostAsJsonAsync("/api/notes", new CreateNoteRequest { Title = $"N{i}", Content = "x" });

        var response = await _client.GetAsync("/api/notes?page=2&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Total-Count").Should().ContainSingle().Which.Should().Be("5");
        (await response.Content.ReadFromJsonAsync<List<NoteDto>>()).Should().HaveCount(2);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
