using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Tests.Integration;

public class NotesEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public NotesEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetNotes_ReturnsOkWithEmptyList_WhenNoNotes()
    {
        var response = await _client.GetAsync("/api/notes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notes = await response.Content.ReadFromJsonAsync<List<NoteDto>>();
        notes.Should().NotBeNull();
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateNote_ReturnsCreated_WithCorrectData()
    {
        var request = new CreateNoteRequest
        {
            Title = "Test Note",
            Content = "# Hello\n\nThis is a **test**.",
            Tags = ["Work"]
        };

        var response = await _client.PostAsJsonAsync("/api/notes", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        note.Should().NotBeNull();
        note!.Title.Should().Be("Test Note");
        note.Content.Should().Be("# Hello\n\nThis is a **test**.");
        note.ContentHtml.Should().Contain("<h1");
        note.Tags.Should().HaveCount(1);
        note.Tags[0].Name.Should().Be("Work");
    }

    [Fact]
    public async Task GetNoteById_ReturnsNote_WhenExists()
    {
        var created = await CreateTestNote("Find Me");

        var response = await _client.GetAsync($"/api/notes/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        note!.Title.Should().Be("Find Me");
    }

    [Fact]
    public async Task GetNoteById_ReturnsNotFound_WhenDoesNotExist()
    {
        var response = await _client.GetAsync($"/api/notes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateNote_ReturnsOk_WithUpdatedData()
    {
        var created = await CreateTestNote("Original");

        var updateRequest = new UpdateNoteRequest
        {
            Title = "Updated",
            Content = "Updated content",
            Tags = []
        };

        var response = await _client.PutAsJsonAsync($"/api/notes/{created.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<NoteDto>();
        updated!.Title.Should().Be("Updated");
        updated.Content.Should().Be("Updated content");
    }

    [Fact]
    public async Task UpdateNote_ReturnsNotFound_WhenDoesNotExist()
    {
        var request = new UpdateNoteRequest { Title = "X", Content = "X", Tags = [] };
        var response = await _client.PutAsJsonAsync($"/api/notes/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteNote_ReturnsNoContent_AndNoteIsGone()
    {
        var created = await CreateTestNote("To Delete");

        var deleteResponse = await _client.DeleteAsync($"/api/notes/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/notes/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetNotes_FiltersByTag_WhenTagProvided()
    {
        await CreateTestNote("Work Note", ["Work"]);
        await CreateTestNote("Personal Note", ["Personal"]);

        var response = await _client.GetAsync("/api/notes?tag=Work");
        var notes = await response.Content.ReadFromJsonAsync<List<NoteDto>>();

        notes.Should().HaveCount(1);
        notes![0].Title.Should().Be("Work Note");
    }

    [Fact]
    public async Task GetNotes_FiltersBySearch_WhenSearchProvided()
    {
        await CreateTestNote("Meeting recap");
        await CreateTestNote("Project ideas");

        var response = await _client.GetAsync("/api/notes?search=recap");
        var notes = await response.Content.ReadFromJsonAsync<List<NoteDto>>();

        notes.Should().HaveCount(1);
        notes![0].Title.Should().Be("Meeting recap");
    }

    [Fact]
    public async Task TogglePin_ReturnsOk_AndNoteIsPinned()
    {
        var note = await CreateTestNote("Pin Me");

        var response = await _client.PatchAsync($"/api/notes/{note.Id}/pin", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PinResult>();
        body!.IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task TogglePin_Twice_ReturnsUnpinned()
    {
        var note = await CreateTestNote("Toggle Pin");

        await _client.PatchAsync($"/api/notes/{note.Id}/pin", null);
        var response = await _client.PatchAsync($"/api/notes/{note.Id}/pin", null);

        var body = await response.Content.ReadFromJsonAsync<PinResult>();
        body!.IsPinned.Should().BeFalse();
    }

    [Fact]
    public async Task TogglePin_ReturnsNotFound_WhenNoteDoesNotExist()
    {
        var response = await _client.PatchAsync($"/api/notes/{Guid.NewGuid()}/pin", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToggleArchive_ReturnsOk_AndNoteIsArchived()
    {
        var note = await CreateTestNote("Archive Me");

        var response = await _client.PatchAsync($"/api/notes/{note.Id}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ArchiveResult>();
        body!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleArchive_ArchivedNote_DisappearsFromActiveList()
    {
        var note = await CreateTestNote("Will Archive");
        await _client.PatchAsync($"/api/notes/{note.Id}/archive", null);

        var listResponse = await _client.GetAsync("/api/notes");
        var notes = await listResponse.Content.ReadFromJsonAsync<List<NoteDto>>();

        notes.Should().NotContain(n => n.Id == note.Id);
    }

    [Fact]
    public async Task ToggleArchive_Twice_RestoresNoteToActiveList()
    {
        var note = await CreateTestNote("Archive Then Restore");
        await _client.PatchAsync($"/api/notes/{note.Id}/archive", null);
        await _client.PatchAsync($"/api/notes/{note.Id}/archive", null);

        var listResponse = await _client.GetAsync("/api/notes");
        var notes = await listResponse.Content.ReadFromJsonAsync<List<NoteDto>>();

        notes.Should().Contain(n => n.Id == note.Id);
    }

    [Fact]
    public async Task GetArchivedNotes_ReturnsArchivedNotes()
    {
        var note = await CreateTestNote("Archived One");
        await _client.PatchAsync($"/api/notes/{note.Id}/archive", null);

        var response = await _client.GetAsync("/api/notes/archived");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notes = await response.Content.ReadFromJsonAsync<List<NoteDto>>();
        notes.Should().Contain(n => n.Id == note.Id);
    }

    [Fact]
    public async Task SetNoteTags_ReturnsOk_WithUpdatedTags()
    {
        var note = await CreateTestNote("Tag Me", ["OldTag"]);
        var allTags = await _client.GetFromJsonAsync<List<TagDto>>("/api/tags");
        var newTag = allTags!.First(t => t.Name == "OldTag");

        var request = new SetNoteTagsRequest { TagIds = [newTag.Id] };
        var response = await _client.PatchAsJsonAsync($"/api/notes/{note.Id}/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<NoteDto>();
        updated!.Tags.Should().HaveCount(1);
        updated.Tags[0].Name.Should().Be("OldTag");
    }

    [Fact]
    public async Task SetNoteTags_ClearsAllTags_WhenEmptyList()
    {
        var note = await CreateTestNote("Clear Tags", ["Tag1", "Tag2"]);

        var request = new SetNoteTagsRequest { TagIds = [] };
        var response = await _client.PatchAsJsonAsync($"/api/notes/{note.Id}/tags", request);

        var updated = await response.Content.ReadFromJsonAsync<NoteDto>();
        updated!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task SetNoteTags_ReturnsNotFound_WhenNoteDoesNotExist()
    {
        var request = new SetNoteTagsRequest { TagIds = [] };
        var response = await _client.PatchAsJsonAsync($"/api/notes/{Guid.NewGuid()}/tags", request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PinnedNotes_AppearFirstInList()
    {
        await CreateTestNote("Regular");
        var pinned = await CreateTestNote("Pinned");
        await _client.PatchAsync($"/api/notes/{pinned.Id}/pin", null);

        var response = await _client.GetAsync("/api/notes");
        var notes = await response.Content.ReadFromJsonAsync<List<NoteDto>>();

        notes![0].Id.Should().Be(pinned.Id);
    }

    private async Task<NoteDto> CreateTestNote(string title, List<string>? tags = null)
    {
        var request = new CreateNoteRequest
        {
            Title = title,
            Content = "Test content",
            Tags = tags ?? []
        };
        var response = await _client.PostAsJsonAsync("/api/notes", request);
        return (await response.Content.ReadFromJsonAsync<NoteDto>())!;
    }

    private record PinResult(bool IsPinned);
    private record ArchiveResult(bool IsArchived);
}
