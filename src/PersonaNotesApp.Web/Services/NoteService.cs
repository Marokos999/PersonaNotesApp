using System.Net.Http.Json;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.Web.Services;

public class NoteService(HttpClient http)
{
    public const int PageSize = 20;

    public async Task<(List<NoteDto> Items, int Total)> GetNotesPageAsync(string? tag, string? search, int page)
    {
        var query = new List<string> { $"page={page}", $"pageSize={PageSize}" };
        if (!string.IsNullOrEmpty(tag)) query.Add($"tag={Uri.EscapeDataString(tag)}");
        if (!string.IsNullOrEmpty(search)) query.Add($"search={Uri.EscapeDataString(search)}");

        var response = await http.GetAsync($"api/notes?{string.Join("&", query)}");
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<List<NoteDto>>() ?? [];
        var total = response.Headers.TryGetValues("X-Total-Count", out var values)
                    && int.TryParse(values.FirstOrDefault(), out var parsed)
            ? parsed
            : items.Count;
        return (items, total);
    }

    public async Task<List<NoteDto>> GetArchivedNotesAsync() =>
        await http.GetFromJsonAsync<List<NoteDto>>("api/notes/archived") ?? [];

    public async Task<NoteDto?> GetNoteByIdAsync(Guid id) =>
        await http.GetFromJsonAsync<NoteDto>($"api/notes/{id}");

    public async Task<NoteDto?> CreateNoteAsync(CreateNoteRequest request)
    {
        var response = await http.PostAsJsonAsync("api/notes", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<NoteDto>() : null;
    }

    public async Task<NoteDto?> UpdateNoteAsync(Guid id, UpdateNoteRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/notes/{id}", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<NoteDto>() : null;
    }

    public async Task<bool> DeleteNoteAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/notes/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TogglePinAsync(Guid id)
    {
        var response = await http.PatchAsync($"api/notes/{id}/pin", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ToggleArchiveAsync(Guid id)
    {
        var response = await http.PatchAsync($"api/notes/{id}/archive", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<NoteDto?> SetNoteTagsAsync(Guid id, List<Guid> tagIds)
    {
        var response = await http.PatchAsJsonAsync($"api/notes/{id}/tags", new SetNoteTagsRequest { TagIds = tagIds });
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<NoteDto>() : null;
    }
}
