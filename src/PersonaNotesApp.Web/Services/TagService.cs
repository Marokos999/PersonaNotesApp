using System.Net.Http.Json;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.Web.Services;

public class TagService(HttpClient http)
{
    public async Task<List<TagDto>> GetAllTagsAsync() =>
        await http.GetFromJsonAsync<List<TagDto>>("api/tags") ?? [];

    public async Task<TagDto?> CreateTagAsync(string name, string color = "#6B7280")
    {
        var response = await http.PostAsJsonAsync("api/tags", new { name, color });
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TagDto>() : null;
    }
}
