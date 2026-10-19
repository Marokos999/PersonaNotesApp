using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.API.Application.Services;

/// <summary>Branding and legal operator details, read from configuration so they can change without a rebuild.</summary>
public sealed class AppInfo(IConfiguration config)
{
    public string Name => Value("App:Name") ?? "PersonaNotes";

    /// <summary>Public address of the site (used in robots.txt and the sitemap), without a trailing slash.</summary>
    public string? PublicUrl => Value("App:PublicUrl")?.TrimEnd('/');

    public string? OperatorName => Value("Legal:OperatorName");
    public string? ContactEmail => Value("Legal:ContactEmail");

    public AppInfoDto ToDto() => new() { Name = Name, OperatorName = OperatorName, ContactEmail = ContactEmail };

    private string? Value(string key) => string.IsNullOrWhiteSpace(config[key]) ? null : config[key]!.Trim();
}
