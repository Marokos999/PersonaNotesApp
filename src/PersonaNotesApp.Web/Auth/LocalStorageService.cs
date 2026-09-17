using Microsoft.JSInterop;

namespace PersonaNotesApp.Web.Auth;

public class LocalStorageService(IJSRuntime js)
{
    public async Task<string?> GetItemAsync(string key) =>
        await js.InvokeAsync<string?>("localStorage.getItem", key);

    public async Task SetItemAsync(string key, string value) =>
        await js.InvokeVoidAsync("localStorage.setItem", key, value);

    public async Task RemoveItemAsync(string key) =>
        await js.InvokeVoidAsync("localStorage.removeItem", key);
}
