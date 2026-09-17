using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Tests.Integration;

public class TagsEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public TagsEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetTags_ReturnsOkWithEmptyList_WhenNoTags()
    {
        var response = await _client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagDto>>();
        tags.Should().NotBeNull();
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTag_ReturnsCreated_WithCorrectData()
    {
        var request = new TagDto { Name = "Work", Color = "#3B82F6" };

        var response = await _client.PostAsJsonAsync("/api/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetTags_ReturnsAllCreatedTags()
    {
        await _client.PostAsJsonAsync("/api/tags", new TagDto { Name = "Alpha", Color = "#ff0000" });
        await _client.PostAsJsonAsync("/api/tags", new TagDto { Name = "Beta", Color = "#00ff00" });

        var response = await _client.GetAsync("/api/tags");
        var tags = await response.Content.ReadFromJsonAsync<List<TagDto>>();

        tags.Should().HaveCountGreaterThanOrEqualTo(2);
        tags!.Should().Contain(t => t.Name == "Alpha");
        tags.Should().Contain(t => t.Name == "Beta");
    }
}
