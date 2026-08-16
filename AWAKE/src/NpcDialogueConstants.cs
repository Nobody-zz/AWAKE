using System;

namespace Awake;

internal static class NpcDialogueConstants
{
    internal const string RouteId = AiTaskConstants.RouteNpcDialogue;
    internal const string PromptId = "awake.npc.v1";
    internal const string PromptVersion = "v1";
    internal const string PromptRevision = "release";
    internal const string OutputContractId = "awake.npc.output.v1";
    internal const int HistoryCapacity = 12;
    internal const int MaxPlayerInputLength = 4000;
    internal const int MaxPromptUtf8Bytes = 32768;
    internal const int LongWaitCancelSeconds = 60;
    internal const string PermissionRouteInvoke = "ai.route.invoke:" + RouteId;
    internal const string PermissionPromptCompile = "prompt.compile:" + PromptId;
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(90);
    internal static readonly string[] AllowedCommandIds = new[]
    {
        AiTaskConstants.RelationshipDeltaCommandId
    };
}
