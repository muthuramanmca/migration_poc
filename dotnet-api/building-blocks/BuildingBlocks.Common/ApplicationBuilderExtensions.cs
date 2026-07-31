using Microsoft.AspNetCore.Builder;

namespace BuildingBlocks.Common;

public static class ApplicationBuilderExtensions
{
    /// <summary>Correlation-ID propagation, then global exception handling. Call first in the pipeline.</summary>
    public static IApplicationBuilder UseBuildingBlocksCommon(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        return app;
    }
}
