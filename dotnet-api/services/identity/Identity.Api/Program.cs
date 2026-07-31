using BuildingBlocks.Common;
using BuildingBlocks.Observability;
using Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBuildingBlocksSerilog(serviceName: "Identity");

builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseBuildingBlocksCommon();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Identity is the token issuer, not (yet) a resource server -- no JwtBearer auth wired here.
// Every other service validates against this JWKS endpoint via BuildingBlocks.Security.
app.MapGet("/.well-known/jwks.json", (RsaSigningKeyProvider keyProvider) => Results.Json(keyProvider.GetPublicJwks()))
   .WithName("GetJwks");

// Real registration/login/refresh endpoints are deferred to Identity's business-logic pass
// (migration plan steps 02-04 for the Identity slice) -- this skeleton only proves the
// infrastructure (DB, key material, JWKS) wires up and the service starts cleanly.

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
