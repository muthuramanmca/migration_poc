using Microsoft.AspNetCore.Builder;
using Serilog;

namespace BuildingBlocks.Observability;

public static class SerilogExtensions
{
    /// <summary>
    /// Replaces the default logging provider with Serilog, enriched with the service name and
    /// (via <c>Enrich.FromLogContext()</c>) the CorrelationId scope pushed by
    /// BuildingBlocks.Common's CorrelationIdMiddleware, and wired to redact any property tagged
    /// <see cref="SensitiveDataAttribute"/>. Call before <c>builder.Build()</c>. Complements
    /// (doesn't replace) the OpenTelemetry logging pipeline wired by Aspire's ServiceDefaults --
    /// both read from the same log events.
    /// </summary>
    public static WebApplicationBuilder AddBuildingBlocksSerilog(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .Destructure.With<SensitiveDataDestructuringPolicy>()
                .WriteTo.Console();
        });

        return builder;
    }
}
