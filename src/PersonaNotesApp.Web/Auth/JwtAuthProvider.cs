using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace PersonaNotesApp.Web.Auth;

public class JwtAuthProvider(LocalStorageService storage) : AuthenticationStateProvider
{
    public const string AccessTokenKey = "access_token";
    public const string RefreshTokenKey = "refresh_token";

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await storage.GetItemAsync(AccessTokenKey);
        if (string.IsNullOrEmpty(token))
            return new AuthenticationState(new ClaimsPrincipal());

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            if (jwt.ValidTo < DateTime.UtcNow)
                return new AuthenticationState(new ClaimsPrincipal());
            var identity = new ClaimsIdentity(jwt.Claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            await storage.RemoveItemAsync(AccessTokenKey);
            await storage.RemoveItemAsync(RefreshTokenKey);
            return new AuthenticationState(new ClaimsPrincipal());
        }
    }

    public async Task NotifyLoginAsync(string accessToken, string refreshToken)
    {
        await storage.SetItemAsync(AccessTokenKey, accessToken);
        await storage.SetItemAsync(RefreshTokenKey, refreshToken);

        var handle = new JwtSecurityTokenHandler();
        var jwt = handle.ReadJwtToken(accessToken);
        var identity = new ClaimsIdentity(jwt.Claims, "jwt");
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity))));
    }

    public async Task NotifyLogoutAsync()
    {
        await storage.RemoveItemAsync(AccessTokenKey);
        await storage.RemoveItemAsync(RefreshTokenKey);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())));
    }
}
