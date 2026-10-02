using Bunit;
using FluentAssertions;
using PersonaNotesApp.Web.Layout;

namespace PersonaNotesApp.Web.Tests.Layout;

public class MarkdownPreviewTests : BunitContext
{
    [Fact]
    public void IsCollapsedUntilToggled()
    {
        var cut = Render<MarkdownPreview>(p => p.Add(c => c.Content, "**bold**"));

        cut.FindAll(".markdown-preview").Should().BeEmpty();

        cut.Find("button").Click();

        cut.Find(".markdown-preview strong").TextContent.Should().Be("bold");
    }

    [Fact]
    public void ReRenders_WhenContentChanges()
    {
        var cut = Render<MarkdownPreview>(p => p.Add(c => c.Content, "# One"));
        cut.Find("button").Click();

        cut.Render(p => p.Add(c => c.Content, "# Two"));

        cut.Find(".markdown-preview h1").TextContent.Should().Be("Two");
    }

    [Fact]
    public void DoesNotRenderRawHtml()
    {
        var cut = Render<MarkdownPreview>(p => p.Add(c => c.Content, "hi <script>alert(1)</script>"));
        cut.Find("button").Click();

        cut.FindAll("script").Should().BeEmpty();
    }

    [Fact]
    public void ShowsHint_WhenContentIsEmpty()
    {
        var cut = Render<MarkdownPreview>(p => p.Add(c => c.Content, ""));
        cut.Find("button").Click();

        cut.Markup.Should().Contain("Nothing to preview yet.");
    }
}
