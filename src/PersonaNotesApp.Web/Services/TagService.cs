using System.Net.Http.Json;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.Web.Services;

public class TagService(HttpClient http)
{
    /// <summary>Raised whenever tags were created, changed or removed so other components can reload.</summary>
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();

    public async Task<List<TagDto>> GetAllTagsAsync() =>
        await http.GetFromJsonAsync<List<TagDto>>("api/tags") ?? [];

    public async Task<TagDto?> CreateTagAsync(string name, string color = "#6B7280")
    {
        var response = await http.PostAsJsonAsync("api/tags", new { name, color });
        if (!response.IsSuccessStatusCode) return null;

        NotifyChanged();
        return await response.Content.ReadFromJsonAsync<TagDto>();
    }

    public async Task<TagDto?> UpdateTagAsync(Guid id, string name, string color)
    {
        var response = await http.PutAsJsonAsync($"api/tags/{id}", new { name, color });
        if (!response.IsSuccessStatusCode) return null;

        NotifyChanged();
        return await response.Content.ReadFromJsonAsync<TagDto>();
    }

    public async Task<bool> DeleteTagAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/tags/{id}");
        if (response.IsSuccessStatusCode) NotifyChanged();
        return response.IsSuccessStatusCode;
    }
}
