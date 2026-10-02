using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Tests.Integration;

/// <summary>User A must never be able to see or modify user B's data.</summary>
public class UserIsolationTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _alice;
    private readonly HttpClient _bob;

    public UserIsolationTests()
    {
        _alice = _factory.CreateClientAs("alice");
        _bob = _factory.CreateClientAs("bob");
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetNotes_DoesNotReturnOtherUsersNotes()
    {
        await CreateNote(_alice, "Alice secret");
        await CreateNote(_bob, "Bob note");

        var notes = await _bob.GetFromJsonAsync<List<NoteDto>>("/api/notes");

        notes.Should().ContainSingle().Which.Title.Should().Be("Bob note");
    }

    [Fact]
    public async Task GetNoteById_ReturnsNotFound_ForOtherUsersNote()
    {
        var note = await CreateNote(_alice, "Alice secret");

        var response = await _bob.GetAsync($"/api/notes/{note.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateNote_ReturnsNotFound_AndKeepsOriginal_ForOtherUsersNote()
    {
        var note = await CreateNote(_alice, "Alice secret");

        var response = await _bob.PutAsJsonAsync($"/api/notes/{note.Id}",
            new UpdateNoteRequest { Title = "Hacked", Content = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var original = await _alice.GetFromJsonAsync<NoteDto>($"/api/notes/{note.Id}");
        original!.Title.Should().Be("Alice secret");
    }

    [Fact]
    public async Task DeleteNote_ReturnsNotFound_AndKeepsNote_ForOtherUsersNote()
    {
        var note = await CreateNote(_alice, "Alice secret");

        var response = await _bob.DeleteAsync($"/api/notes/{note.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _alice.GetAsync($"/api/notes/{note.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PinAndArchive_ReturnNotFound_ForOtherUsersNote()
    {
        var note = await CreateNote(_alice, "Alice secret");

        (await _bob.PatchAsync($"/api/notes/{note.Id}/pin", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _bob.PatchAsync($"/api/notes/{note.Id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetArchivedNotes_DoesNotReturnOtherUsersNotes()
    {
        var note = await CreateNote(_alice, "Alice archived");
        await _alice.PatchAsync($"/api/notes/{note.Id}/archive", null);

        var archived = await _bob.GetFromJsonAsync<List<NoteDto>>("/api/notes/archived");

        archived.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTags_DoesNotReturnOtherUsersTags()
    {
        await _alice.PostAsJsonAsync("/api/tags", new TagDto { Name = "AliceTag", Color = "#112233" });

        var tags = await _bob.GetFromJsonAsync<List<TagDto>>("/api/tags");

        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task SetNoteTags_CannotAttachOtherUsersTag()
    {
        var aliceTag = await (await _alice.PostAsJsonAsync("/api/tags", new TagDto { Name = "AliceTag", Color = "#112233" }))
            .Content.ReadFromJsonAsync<TagDto>();
        var bobNote = await CreateNote(_bob, "Bob note");

        var response = await _bob.PatchAsJsonAsync($"/api/notes/{bobNote.Id}/tags",
            new SetNoteTagsRequest { TagIds = [aliceTag!.Id] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<NoteDto>();
        updated!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAndDeleteTag_ReturnNotFound_ForOtherUsersTag()
    {
        var aliceTag = await (await _alice.PostAsJsonAsync("/api/tags", new TagDto { Name = "AliceTag", Color = "#112233" }))
            .Content.ReadFromJsonAsync<TagDto>();

        var put = await _bob.PutAsJsonAsync($"/api/tags/{aliceTag!.Id}", new TagDto { Name = "Stolen", Color = "#000000" });
        var delete = await _bob.DeleteAsync($"/api/tags/{aliceTag.Id}");

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var tags = await _alice.GetFromJsonAsync<List<TagDto>>("/api/tags");
        tags.Should().ContainSingle().Which.Name.Should().Be("AliceTag");
    }

    private static async Task<NoteDto> CreateNote(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/notes",
            new CreateNoteRequest { Title = title, Content = "content" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NoteDto>())!;
    }
}
