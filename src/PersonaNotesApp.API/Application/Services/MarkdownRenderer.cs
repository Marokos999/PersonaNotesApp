using AngleSharp.Dom;
using Ganss.Xss;
using Markdig;

namespace PersonaNotesApp.API.Application.Services;

/// <summary>Renders Markdown to HTML and strips anything that could execute script in the browser.</summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseTaskLists()
        .Build();

    /// <summary>Bump when rendering/sanitizing rules change; stored notes with an older version are re-rendered at startup.</summary>
    public const int Version = 1;

    private static readonly ThreadLocal<HtmlSanitizer> Sanitizer = new(CreateSanitizer);

    public static string ToHtml(string markdown) =>
        Sanitizer.Value!.Sanitize(Markdown.ToHtml(markdown, Pipeline));

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        // Task lists render as <input type="checkbox" disabled>; nothing else is allowed through.
        sanitizer.AllowedTags.Add("input");
        foreach (var attr in new[] { "type", "checked", "disabled", "class" })
            sanitizer.AllowedAttributes.Add(attr);

        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { LocalName: "input" } input && input.GetAttribute("type") != "checkbox")
                input.Remove();
        };

        return sanitizer;
    }
}
