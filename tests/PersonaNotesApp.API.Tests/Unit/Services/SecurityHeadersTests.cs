using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using PersonaNotesApp.API.Infrastructure;

namespace PersonaNotesApp.API.Tests.Unit.Services;

public sealed class SecurityHeadersTests : IDisposable
{
    private readonly string _webRoot = Directory.CreateTempSubdirectory("pna-webroot-").FullName;

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    [Fact]
    public void Csp_AllowsTheInlineImportMap_ByItsHash()
    {
        const string importMap = "{\n  \"imports\": { \"./_framework/dotnet.js\": \"./_framework/dotnet.abc123.js\" }\n}";
        File.WriteAllText(Path.Combine(_webRoot, "index.html"),
            $"<html><head><script src=\"js/theme.js\"></script><script type=\"importmap\">{importMap}</script></head></html>");
        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(importMap)));

        var csp = SecurityHeaders.BuildCsp(new PhysicalFileProvider(_webRoot));

        csp.Should().Contain($"script-src 'self' 'wasm-unsafe-eval' 'sha256-{expectedHash}'");
    }

    [Fact]
    public void Csp_HasNoHash_WhenTheImportMapIsEmptyOrMissing()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<html><head><script type=\"importmap\"></script></head></html>");

        SecurityHeaders.BuildCsp(new PhysicalFileProvider(_webRoot)).Should().NotContain("sha256-");
        SecurityHeaders.BuildCsp(new NullFileProvider()).Should().NotContain("sha256-");
    }

    [Fact]
    public void Csp_NeverAllowsUnsafeScripts()
    {
        var csp = SecurityHeaders.BuildCsp(new NullFileProvider());

        var scriptDirective = csp.Split("; ").Single(d => d.StartsWith("script-src"));
        scriptDirective.Should().NotContain("unsafe-inline").And.NotContain("unsafe-eval ").And.NotContain("https:");
    }
}
