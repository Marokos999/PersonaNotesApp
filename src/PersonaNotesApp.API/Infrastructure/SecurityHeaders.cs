using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;

namespace PersonaNotesApp.API.Infrastructure;

/// <summary>Response headers that harden the browser side: CSP, clickjacking, MIME sniffing, referrer and feature policies.</summary>
public static partial class SecurityHeaders
{
    public static IApplicationBuilder UseSecurityHeaders(this WebApplication app)
    {
        // The Development CSP would break Scalar (inline scripts, CDN assets), so it is only sent elsewhere.
        var csp = app.Environment.IsDevelopment() ? null : BuildCsp(app.Environment.WebRootFileProvider);

        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                if (csp is not null)
                    headers["Content-Security-Policy"] = csp;
                return Task.CompletedTask;
            });
            await next();
        });
    }

    /// <summary>
    /// Scripts are limited to this origin plus WebAssembly compilation. Blazor's publish step inlines an import map into
    /// index.html, so that one inline script is allowed by its hash instead of opening script-src to 'unsafe-inline'.
    /// Styles keep 'unsafe-inline' because Blazor sets style attributes (tag colors) from component code.
    /// </summary>
    public static string BuildCsp(IFileProvider webRoot)
    {
        var scriptSrc = "'self' 'wasm-unsafe-eval'";
        var importMapHash = ImportMapHash(webRoot.GetFileInfo("index.html"));
        if (importMapHash is not null)
            scriptSrc += $" 'sha256-{importMapHash}'";

        return string.Join("; ",
            "default-src 'self'",
            $"script-src {scriptSrc}",
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
            "font-src 'self' https://fonts.gstatic.com",
            "img-src 'self' data: https:",
            "connect-src 'self'",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'");
    }

    private static string? ImportMapHash(IFileInfo indexHtml)
    {
        if (!indexHtml.Exists) return null;

        using var reader = new StreamReader(indexHtml.CreateReadStream());
        var match = ImportMapRegex().Match(reader.ReadToEnd());
        if (!match.Success || match.Groups[1].Value.Length == 0) return null;

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(match.Groups[1].Value)));
    }

    [GeneratedRegex("""<script\s+type="importmap"\s*>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex ImportMapRegex();
}
