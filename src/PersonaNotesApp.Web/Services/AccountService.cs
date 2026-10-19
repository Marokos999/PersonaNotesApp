using System.Net;
using System.Net.Http.Json;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.Web.Services;

public record ExportFile(byte[] Content, string FileName);

public class AccountService(HttpClient http)
{
    private const string DefaultExportName = "personanotes-export.json";

    /// <summary>Downloads everything the server stores about the signed-in user, or null if that failed.</summary>
    public async Task<ExportFile?> ExportAsync()
    {
        using var response = await http.GetAsync("api/account/export");
        if (!response.IsSuccessStatusCode) return null;

        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"');
        return new ExportFile(await response.Content.ReadAsByteArrayAsync(), string.IsNullOrWhiteSpace(fileName) ? DefaultExportName : fileName);
    }

    /// <summary>Permanently deletes the account after confirming the password. Returns an error message, or null on success.</summary>
    public async Task<string?> DeleteAsync(string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "api/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest { Password = password })
        };
        using var response = await http.SendAsync(request);

        return response.StatusCode switch
        {
            HttpStatusCode.NoContent or HttpStatusCode.OK => null,
            HttpStatusCode.BadRequest => "Incorrect password.",
            HttpStatusCode.TooManyRequests => "Too many attempts. Try again in a minute.",
            _ => "The account could not be deleted. Please try again."
        };
    }
}
