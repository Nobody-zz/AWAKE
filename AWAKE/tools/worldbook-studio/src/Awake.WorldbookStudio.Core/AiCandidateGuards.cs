using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class AiCandidateGuards
{
    public static void EnsureApplyAllowed(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document["sources"] is not null)
            throw new InvalidOperationException("WB-EDITOR-SOURCE-403: 来源型内容不能应用 AI 候选，请在高级模式中手工维护。");
    }
}
