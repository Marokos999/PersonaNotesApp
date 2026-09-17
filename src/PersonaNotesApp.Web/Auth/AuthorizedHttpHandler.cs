using System.Net.Http.Headers;

namespace PersonaNotesApp.Web.Auth;

public class AuthorizedHttpHandler(LocalStorageService storage) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await storage.GetItemAsync(JwtAuthProvider.AccessTokenKey);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
