using System.Security;
using System.Text;

namespace PersonaNotesApp.API.Application.Services;

public static class SeoDocuments
{
    // Only pages meant for search engines; everything behind login is excluded.
    public static readonly string[] PublicPaths = ["/", "/login", "/register", "/privacy", "/terms"];

    public static string Robots(string origin) => string.Join('\n',
        "User-agent: *",
        "Disallow: /api/",
        "Disallow: /notes",
        "Disallow: /tags",
        "Disallow: /account",
        $"Sitemap: {origin}/sitemap.xml") + "\n";

    public static string Sitemap(string origin)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        foreach (var path in PublicPaths)
            sb.Append("  <url><loc>").Append(SecurityElement.Escape(origin + path)).Append("</loc></url>\n");
        return sb.Append("</urlset>\n").ToString();
    }
}
