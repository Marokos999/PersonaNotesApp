using System.Net;
using System.Net.Http.Json;

namespace PersonaNotesApp.Web.Tests.Support;

/// <summary>Routes requests ("METHOD /path") to canned responses and records what was sent.</summary>
public class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes = new();

    public List<RecordedRequest> Requests { get; } = [];

    public StubHttpHandler On(string method, string path, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        routes[$"{method} {path}"] = respond;
        return this;
    }

    public StubHttpHandler On(string method, string path, HttpStatusCode status) =>
        On(method, path, _ => new HttpResponseMessage(status));

    public StubHttpHandler OnJson<T>(string method, string path, T body, HttpStatusCode status = HttpStatusCode.OK) =>
        On(method, path, _ => new HttpResponseMessage(status) { Content = JsonContent.Create(body) });

    public HttpClient CreateClient() => new(this) { BaseAddress = new Uri("http://localhost/") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method.Method, request.RequestUri!.AbsolutePath, body,
            request.Headers.Authorization?.ToString()));

        return routes.TryGetValue($"{request.Method.Method} {request.RequestUri!.AbsolutePath}", out var respond)
            ? respond(request)
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

public record RecordedRequest(string Method, string Path, string? Body, string? Authorization);
