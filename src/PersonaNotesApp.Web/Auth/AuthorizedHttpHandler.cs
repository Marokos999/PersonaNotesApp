using System.Net;
using System.Net.Http.Headers;

namespace PersonaNotesApp.Web.Auth;

/// <summary>Adds the bearer token and, on a 401, refreshes the session once and replays the request.</summary>
public class AuthorizedHttpHandler(LocalStorageService storage, TokenRefresher refresher, JwtAuthProvider auth)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer the body so the request can be replayed after a refresh.
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);

        var token = await storage.GetItemAsync(JwtAuthProvider.AccessTokenKey);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized || IsAuthEndpoint(request.RequestUri))
            return response;

        var newToken = await refresher.RefreshAsync(token, cancellationToken);
        if (newToken is null)
        {
            await auth.NotifyLogoutAsync();
            return response;
        }

        response.Dispose();
        using var retry = await CloneAsync(request, cancellationToken);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static bool IsAuthEndpoint(Uri? uri) =>
        uri is not null && uri.AbsolutePath.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase);

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(ct);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
