namespace PersonaNotesApp.Shared.DTOs;

/// <summary>Public, configuration-driven details of this installation (shown on the privacy policy and terms pages).</summary>
public class AppInfoDto
{
    public string Name { get; set; } = "PersonaNotes";
    public string? OperatorName { get; set; }
    public string? ContactEmail { get; set; }
    public bool LegalConfigured => !string.IsNullOrWhiteSpace(OperatorName) && !string.IsNullOrWhiteSpace(ContactEmail);
}
