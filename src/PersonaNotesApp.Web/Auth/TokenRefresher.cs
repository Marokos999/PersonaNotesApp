using System.Net.Http.Json;
using PersonaNotesApp.Shared.DTOs;

namespace PersonaNotesApp.Web.Auth;

/// <summary>Exchanges the stored refresh token for a new token pair. Concurrent callers share one refresh.</summary>
public class TokenRefresher(LocalStorageService storage, IHttpClientFactory httpFactory)
{
    public const string ClientName = "AuthApi";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <param name="staleAccessToken">The access token that was rejected (or null if none was sent).</param>
    /// <returns>The fresh access token, or null if the session can no longer be renewed.</returns>
    public async Task<string?> RefreshAsync(string? staleAccessToken, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            // Another request may have refreshed while we waited for the gate.
            var current = await storage.GetItemAsync(JwtAuthProvider.AccessTokenKey);
            if (!string.IsNullOrEmpty(current) && current != staleAccessToken)
                return current;

            var refreshToken = await storage.GetItemAsync(JwtAuthProvider.RefreshTokenKey);
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            try
            {
                var client = httpFactory.CreateClient(ClientName);
                using var response = await client.PostAsJsonAsync(
                    "api/auth/refresh", new RefreshRequest { RefreshToken = refreshToken }, ct);

                if (!response.IsSuccessStatusCode)
                {
                    await storage.RemoveItemAsync(JwtAuthProvider.AccessTokenKey);
                    await storage.RemoveItemAsync(JwtAuthProvider.RefreshTokenKey);
                    return null;
                }

                var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
                if (tokens is null) return null;

                await storage.SetItemAsync(JwtAuthProvider.AccessTokenKey, tokens.AccessToken);
                await storage.SetItemAsync(JwtAuthProvider.RefreshTokenKey, tokens.RefreshToken);
                return tokens.AccessToken;
            }
            catch (HttpRequestException)
            {
                // Network problem: keep the tokens so the user can retry later.
                return null;
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}
