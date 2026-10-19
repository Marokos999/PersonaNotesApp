using System.Net.Http.Json;
using System.Text.Json;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.Web.Services;

/// <summary>Loads the operator details configured on the server; they are shown on the privacy policy and terms pages.</summary>
public class AppInfoService(HttpClient http)
{
    private AppInfoDto? cached;

    /// <summary>Never throws: if the details cannot be loaded, an unconfigured instance is returned and the next call retries.</summary>
    public async Task<AppInfoDto> GetAsync()
    {
        if (cached is not null) return cached;

        try
        {
            cached = await http.GetFromJsonAsync<AppInfoDto>("api/app-info");
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException)
        {
            // Fall through to the default below.
        }

        return cached ?? new AppInfoDto();
    }
}
