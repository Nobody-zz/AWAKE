using System.Net;
using Microsoft.AspNetCore.Http;

namespace PersonaWorkbench.Web;

public static class WorkbenchWebPolicy
{
    public const int DefaultPort = 51337;
    public const string ContentSecurityPolicy = "default-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; connect-src 'self'; img-src 'self'; script-src 'self'; style-src 'self'";
    public const string ReferrerPolicy = "no-referrer";

    public static bool IsLoopback(IPAddress? address)
    {
        return address != null && IPAddress.IsLoopback(address);
    }

    public static bool IsAllowedRequest(HttpContext context)
    {
        return IsLoopback(context.Connection.RemoteIpAddress)
            && string.Equals(context.Request.Host.Host, "127.0.0.1", StringComparison.Ordinal);
    }

    public static void ApplySecurityHeaders(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;
        response.Headers["Referrer-Policy"] = ReferrerPolicy;
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    }
}
