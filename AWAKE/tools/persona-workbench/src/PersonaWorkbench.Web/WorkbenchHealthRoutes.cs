using Microsoft.AspNetCore.Routing;

namespace PersonaWorkbench.Web;

public static class WorkbenchHealthRoutes
{
    public static void Map(IEndpointRouteBuilder endpoints, Func<WorkbenchHealth> health)
    {
        endpoints.MapGet("/health", () => Results.Ok(health()));
        endpoints.MapGet("/api/health", () => Results.Ok(health()));
    }
}
