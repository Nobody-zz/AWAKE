using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Awake.WorldbookStudio.Web;

public interface IAuthorityFailureCorrelationIds
{
    string Create();
}

public sealed class AuthorityFailureCorrelationIds : IAuthorityFailureCorrelationIds
{
    public string Create() => Guid.NewGuid().ToString("D").ToLowerInvariant();
}

public enum AuthorityFailureContext
{
    Preflight,
    MutationUnknown,
    RetiredRoute
}

public sealed record AuthorityHttpErrorProjection(
    int StatusCode,
    string Error,
    string Message,
    string SideEffect,
    string CorrelationId)
{
    public static AuthorityHttpErrorProjection Project(Exception exception, AuthorityFailureContext context)
        => Project(exception, context, new AuthorityFailureCorrelationIds());

    public static AuthorityHttpErrorProjection Project(Exception exception, AuthorityFailureContext context, IAuthorityFailureCorrelationIds correlationIds)
    {
        var code = ExtractCode(exception);
        var known = StatusFor(code);
        var normalizedCode = known.HasValue ? code : "WB-AUTHORITY-UNKNOWN-500";
        var status = known ?? StatusCodes.Status500InternalServerError;
        var sideEffect = context == AuthorityFailureContext.Preflight || context == AuthorityFailureContext.RetiredRoute ? "none" : "unknown";
        return new AuthorityHttpErrorProjection(status, normalizedCode, SafeMessage(normalizedCode), sideEffect, correlationIds.Create());
    }

    public static async Task WriteAsync(HttpContext context, Exception exception, AuthorityFailureContext failureContext)
    {
        var projection = Project(exception, failureContext);
        Log(context, exception, failureContext, projection);

        if (context.Response.HasStarted)
        {
            context.Abort();
            return;
        }

        context.Response.Headers["X-AWAKE-Correlation-Id"] = projection.CorrelationId;
        context.Response.StatusCode = projection.StatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new
        {
            ok = false,
            error = projection.Error,
            message = projection.Message,
            side_effect = projection.SideEffect,
            correlation_id = projection.CorrelationId
        });
    }

    public static void Log(HttpContext context, Exception exception, AuthorityFailureContext failureContext, AuthorityHttpErrorProjection projection)
    {
        var loggerFactory = context.RequestServices?.GetService<ILoggerFactory>();
        loggerFactory?.CreateLogger("Awake.WorldbookStudio.AuthorityHttp").LogError(
            exception,
            "Authority HTTP failure {CorrelationId} {Error} {FailureContext} {Method} {Path}",
            projection.CorrelationId,
            projection.Error,
            failureContext,
            context.Request.Method,
            context.Request.Path.Value);
    }

    private static string ExtractCode(Exception exception)
    {
        var message = exception.Message ?? string.Empty;
        var code = message.Split(':', 2)[0].Trim();
        return code.StartsWith("WB-AUTHORITY-", StringComparison.Ordinal) ? code : string.Empty;
    }

    private static int? StatusFor(string code)
        => code switch
        {
            "WB-AUTHORITY-400" => StatusCodes.Status400BadRequest,
            "WB-AUTHORITY-404" or "WB-AUTHORITY-SELECTION-404" or "WB-AUTHORITY-STAGING-404" => StatusCodes.Status404NotFound,
            "WB-AUTHORITY-CAS-409" or "WB-AUTHORITY-STAGING-409" or "WB-AUTHORITY-OPERATION-409" or "WB-AUTHORITY-APPROVAL-409" or "WB-AUTHORITY-PROOF-409" or "WB-AUTHORITY-CLOSURE-409" or "WB-AUTHORITY-COMPILE-409" or "WB-AUTHORITY-POINTER-409" or "WB-AUTHORITY-SAFEID-409" => StatusCodes.Status409Conflict,
            "WB-AUTHORITY-422" or "WB-AUTHORITY-DOCUMENT-422" or "WB-AUTHORITY-SELECTION-422" or "WB-AUTHORITY-TIER-422" or "WB-AUTHORITY-COMPILE-422" => StatusCodes.Status422UnprocessableEntity,
            "WB-AUTHORITY-PROOF-403" => StatusCodes.Status403Forbidden,
            "WB-AUTHORITY-MUTATION-UNKNOWN" => StatusCodes.Status503ServiceUnavailable,
            "WB-AUTHORITY-LEGACY-410" => StatusCodes.Status410Gone,
            _ => null
        };

    private static string SafeMessage(string code)
        => code switch
        {
            "WB-AUTHORITY-400" => "授权请求无效，请检查请求内容。",
            "WB-AUTHORITY-404" => "找不到授权记录。",
            "WB-AUTHORITY-SELECTION-404" => "找不到指定的选择快照。",
            "WB-AUTHORITY-STAGING-404" => "找不到指定的暂存产物。",
            "WB-AUTHORITY-CAS-409" => "授权档案已发生变化，请重新读取后重试。",
            "WB-AUTHORITY-SAFEID-409" => "历史授权记录或存储标识发生冲突，请重新读取后重试。",
            "WB-AUTHORITY-STAGING-409" => "暂存产物已发生变化，请重新生成。",
            "WB-AUTHORITY-OPERATION-409" => "该操作已存在或当前状态不允许重复执行。",
            "WB-AUTHORITY-APPROVAL-409" => "审核状态已变化，请重新读取审核结果。",
            "WB-AUTHORITY-PROOF-409" => "授权证明已变化或已被使用。",
            "WB-AUTHORITY-CLOSURE-409" => "授权闭环尚未完成或已发生变化。",
            "WB-AUTHORITY-COMPILE-409" => "编译请求与当前授权状态不一致。",
            "WB-AUTHORITY-POINTER-409" => "当前发布指针已变化，请重新读取后重试。",
            "WB-AUTHORITY-422" or "WB-AUTHORITY-DOCUMENT-422" or "WB-AUTHORITY-SELECTION-422" or "WB-AUTHORITY-TIER-422" or "WB-AUTHORITY-COMPILE-422" => "授权请求未通过结构或语义检查。",
            "WB-AUTHORITY-PROOF-403" => "授权证明无效或无权执行该操作。",
            "WB-AUTHORITY-LEGACY-410" => "此旧接口已停用，请使用当前工作流。",
            "WB-AUTHORITY-MUTATION-UNKNOWN" => "写入结果暂时无法确认，请检查操作状态后再决定是否重试。",
            "WB-AUTHORITY-UNKNOWN-500" => "工作室暂时无法完成该操作，请稍后重试。",
            _ => "工作室暂时无法完成该操作，请稍后重试。"
        };
}
