using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PersonaNotesApp.Web;
using PersonaNotesApp.Web.Auth;
using PersonaNotesApp.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthProvider>();
builder.Services.AddScoped<JwtAuthProvider>(sp =>
    (JwtAuthProvider)sp.GetRequiredService<AuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

var apiBaseUrlStr = builder.Configuration["ApiBaseUrl"];
var apiBaseUrl = !string.IsNullOrEmpty(apiBaseUrlStr) && Uri.IsWellFormedUriString(apiBaseUrlStr, UriKind.Absolute)
    ? apiBaseUrlStr
    : builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped<TokenRefresher>();
builder.Services.AddScoped<AuthorizedHttpHandler>();
// Plain client (no auth handler) used only to call /api/auth/refresh.
builder.Services.AddHttpClient(TokenRefresher.ClientName, client =>
    client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient("API", client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthorizedHttpHandler>();
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

builder.Services.AddScoped<NoteService>();
builder.Services.AddScoped<TagService>();

await builder.Build().RunAsync();
