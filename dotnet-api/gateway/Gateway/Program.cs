using System.Threading.RateLimiting;
using BuildingBlocks.Common;
using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBuildingBlocksSerilog(serviceName: "Gateway");

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

// Edge JWT validation. Every downstream service also validates independently (zero trust --
// the network boundary alone is never trusted), but the Gateway is still the first line of
// defense and where rate limiting/security headers apply uniformly across all routes.
builder.Services.AddBuildingBlocksJwtAuthentication(builder.Configuration);

// Mitigates credential-stuffing/account-takeover against the identity route specifically --
// an airline-specific concern given loyalty accounts and saved profile data. Attached to the
// identity-route via ReverseProxy:Routes:identity-route:RateLimiterPolicy in appsettings.json
// once that route carries real register/login endpoints.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseBuildingBlocksCommon();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Security headers -- ASP.NET Core doesn't set these by convention the way Spring Security does.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
    if (context.Request.IsHttps)
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapReverseProxy();

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
