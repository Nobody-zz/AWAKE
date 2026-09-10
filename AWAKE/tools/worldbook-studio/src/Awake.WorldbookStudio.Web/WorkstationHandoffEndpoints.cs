using System.Text.Json;
using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Web;

internal static class WorkstationHandoffEndpoints
{
    private const string Prefix = "/api/integration/persona/handoff";

    internal static void Map(
        WebApplication app,
        WebAiSessionStore sessions,
        WorkstationHandoffStore handoffs,
        WorkstationHandoffFaultInjection faultInjection)
    {
        app.MapPost($"{Prefix}/import", (HttpContext context, JsonObject? request) =>
            Execute(context, sessions, () =>
            {
                RequireFields(request, ["envelope"]);
                var envelope = request!["envelope"] as JsonObject
                    ?? throw new WorkstationHandoffException("WB-HANDOFF-400", "envelope 必须为对象。", 400, "envelope");
                var result = handoffs.Import(envelope);
                return Results.Json(Success(new JsonObject
                {
                    ["handoff_id"] = result.HandoffId,
                    ["receipt_id"] = ReceiptId(result),
                    ["lifecycle_status"] = Status(result),
                    ["consumer_review_status"] = "needs_review",
                    ["review_only"] = true,
                    ["document_id"] = result.Envelope["document_id"]?.DeepClone(),
                    ["revision"] = result.Envelope["revision"]?.DeepClone(),
                    ["content_sha256"] = result.Envelope["content_sha256"]?.DeepClone()
                }));
            }));

        app.MapPost($"{Prefix}/{{handoffId}}/accept", (HttpContext context, string handoffId, JsonObject? request) =>
            Execute(context, sessions, () =>
            {
                RequireFields(request, ["receipt_id", "expected_status"]);
                var result = handoffs.Accept(
                    handoffId,
                    RequiredString(request!, "receipt_id"),
                    RequiredString(request!, "expected_status"));
                return Results.Json(Success(new JsonObject
                {
                    ["handoff_id"] = result.HandoffId,
                    ["receipt_id"] = ReceiptId(result),
                    ["lifecycle_status"] = Status(result),
                    ["accepted_at_utc"] = result.Receipt["accepted_at_utc"]?.DeepClone()
                }));
            }));

        app.MapPost($"{Prefix}/{{handoffId}}/consume", (HttpContext context, string handoffId, JsonObject? request) =>
            Execute(context, sessions, () =>
            {
                RequireFields(request, ["receipt_id", "expected_status"]);
                var result = handoffs.Consume(
                    handoffId,
                    RequiredString(request!, "receipt_id"),
                    RequiredString(request!, "expected_status"),
                    faultInjection.ShouldInjectConsumeInDoubt(context.Request));
                return Results.Json(Success(new JsonObject
                {
                    ["handoff_id"] = result.HandoffId,
                    ["receipt_id"] = ReceiptId(result),
                    ["lifecycle_status"] = Status(result),
                    ["draft_id"] = result.DraftId is null ? null : result.DraftId,
                    ["draft_status"] = result.DraftStatus ?? "needs_review",
                    ["review_only"] = true
                }));
            }));

        app.MapPost($"{Prefix}/{{handoffId}}/recover", (HttpContext context, string handoffId, JsonObject? request) =>
            Execute(context, sessions, () =>
            {
                RequireFields(request, ["receipt_id", "action"], "reason");
                var result = handoffs.Recover(
                    handoffId,
                    RequiredString(request!, "receipt_id"),
                    RequiredString(request!, "action"),
                    OptionalString(request!, "reason"));
                return Results.Json(Success(new JsonObject
                {
                    ["handoff_id"] = result.HandoffId,
                    ["receipt_id"] = ReceiptId(result),
                    ["lifecycle_status"] = Status(result),
                    ["draft_id"] = result.DraftId is null ? null : result.DraftId,
                    ["draft_status"] = result.DraftStatus,
                    ["review_only"] = true,
                    ["recovery_required"] = result.Receipt["recovery_required"]?.DeepClone()
                }));
            }));

        app.MapGet($"{Prefix}/{{handoffId}}", (HttpContext context, string handoffId) =>
            Execute(context, sessions, () =>
            {
                var result = handoffs.Get(handoffId);
                return Results.Json(Success(new JsonObject
                {
                    ["envelope"] = result.Envelope.DeepClone(),
                    ["receipt"] = result.Receipt.DeepClone(),
                    ["lifecycle_status"] = Status(result),
                    ["consumer_review_status"] = result.Receipt["consumer_review_status"]?.DeepClone(),
                    ["recovery_required"] = result.Receipt["recovery_required"]?.DeepClone()
                }));
            }));
    }

    private static IResult Execute(
        HttpContext context,
        WebAiSessionStore sessions,
        Func<IResult> action)
    {
        sessions.RequireSession(context);
        try
        {
            return action();
        }
        catch (WorkstationHandoffException ex)
        {
            return Failure(ex);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("WB-HANDOFF-", StringComparison.Ordinal))
        {
            return Failure(new WorkstationHandoffException("WB-HANDOFF-422", ex.Message, 422));
        }
    }

    private static JsonObject Success(JsonObject data)
        => new() { ["ok"] = true, ["data"] = data };

    private static IResult Failure(WorkstationHandoffException exception)
    {
        var body = new JsonObject
        {
            ["ok"] = false,
            ["error"] = exception.Code,
            ["message"] = exception.Message,
            ["correlation_id"] = "corr-" + Guid.NewGuid().ToString("N")
        };
        if (!string.IsNullOrWhiteSpace(exception.Path)) body["path"] = exception.Path;
        return Results.Json(body, statusCode: exception.StatusCode);
    }

    private static void RequireFields(JsonObject? request, string[] required, params string[] optional)
    {
        if (request is null)
            throw new WorkstationHandoffException("WB-HANDOFF-400", "请求体不能为空。", 400);
        var allowed = new HashSet<string>(required.Concat(optional), StringComparer.Ordinal);
        foreach (var property in request)
            if (!allowed.Contains(property.Key))
                throw new WorkstationHandoffException("WB-HANDOFF-400", "请求包含未冻结字段。", 400, property.Key);
        foreach (var property in required)
            if (request[property] is null)
                throw new WorkstationHandoffException("WB-HANDOFF-400", $"缺少字段：{property}。", 400, property);
    }

    private static string RequiredString(JsonObject request, string property)
    {
        if (request[property] is not JsonValue value || !value.TryGetValue<string>(out var result) || string.IsNullOrWhiteSpace(result))
            throw new WorkstationHandoffException("WB-HANDOFF-400", $"{property} 必须为非空字符串。", 400, property);
        return result;
    }

    private static string? OptionalString(JsonObject request, string property)
        => request[property] is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;

    private static string ReceiptId(WorkstationHandoffOperation result)
        => result.Receipt["receipt_id"]!.GetValue<string>();

    private static string Status(WorkstationHandoffOperation result)
        => result.Receipt["lifecycle_status"]!.GetValue<string>();
}
