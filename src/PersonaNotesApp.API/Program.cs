using System.Text;
using System.Threading.RateLimiting;
using Carter;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PersonaNotesApp.API.Application.Interfaces;
using PersonaNotesApp.API.Application.Services;
using PersonaNotesApp.API.Domain;
using PersonaNotesApp.API.Infrastructure;
using PersonaNotesApp.API.Infrastructure.Repositories;
using PersonaNotesApp.API.Modules;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("PersonaNotesApp.API"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation());
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    otel.UseOtlpExporter();

if (!isTesting)
{
    var jwtSecret = builder.Configuration["Jwt:Secret"];
    if (string.IsNullOrWhiteSpace(jwtSecret)
        || jwtSecret.Length < 32
        || jwtSecret.StartsWith("CHANGE-ME", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Jwt:Secret is missing, too short (min 32 chars) or still a placeholder. " +
            "Set the Jwt__Secret environment variable (see .env.example).");
    }

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                    .AddEntityFrameworkStores<AppDbContext>()
                    .AddDefaultTokenProviders();

    builder.Services.AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

    builder.Services.AddScoped<TokenService>();
}

builder.Services.AddAuthorization();
builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddCarter();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

// The Blazor client is normally served from this same origin, so CORS is only needed
// when the API is called from another origin (e.g. `dotnet watch` on a different port).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("X-Total-Count");
    });
});

// Brute-force protection for /api/auth/*; partitioned per client IP.
var authPermitLimit = isTesting ? int.MaxValue : builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthModule.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Behind a reverse proxy the client IP (used by the rate limiter) is only available via X-Forwarded-For.
// Opt-in, because trusting the header without a proxy in front would let clients spoof their IP.
var useForwardedHeaders = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Trust the proxy in front of the app (container networks use changing addresses).
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (useForwardedHeaders)
    app.UseForwardedHeaders();

if (!isTesting)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var refreshed = await NoteHtmlRefresher.RefreshAsync(db);
    if (refreshed > 0)
        app.Logger.LogInformation("Re-rendered {Count} notes with the current Markdown renderer", refreshed);
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

var mimeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
mimeProvider.Mappings[".dat"] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = mimeProvider,
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/_framework"))
            ctx.Context.Response.Headers["Blazor-Environment"] = app.Environment.EnvironmentName;
    }
});
app.UseCors("BlazorPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapCarter();
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }
