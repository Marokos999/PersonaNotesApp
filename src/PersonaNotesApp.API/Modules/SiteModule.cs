using Carter;
using PersonaNotesApp.API.Application.Services;

namespace PersonaNotesApp.API.Modules;

/// <summary>Anonymous, site-wide endpoints: installation details for the legal pages, robots.txt and the sitemap.</summary>
public class SiteModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/app-info", (AppInfo info) => Results.Ok(info.ToDto()));

        app.MapGet("/robots.txt", (HttpContext http, AppInfo info) =>
            Results.Text(SeoDocuments.Robots(Origin(http, info)), "text/plain"));

        app.MapGet("/sitemap.xml", (HttpContext http, AppInfo info) =>
            Results.Text(SeoDocuments.Sitemap(Origin(http, info)), "application/xml"));
    }

    private static string Origin(HttpContext http, AppInfo info) =>
        info.PublicUrl ?? $"{http.Request.Scheme}://{http.Request.Host}";
}
