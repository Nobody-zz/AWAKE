namespace Awake;

internal sealed class SceneDialogueModePolicy
{
    internal const string PromptId = "awake.scene_shout.v1";
    internal const string PromptVersion = "v1";
    internal const string PromptRevision = "release";
    internal const string OutputContractId = "awake.scene_shout.output.v1";

    internal bool AllowsNpcMemory => false;
    internal bool AllowsRelationshipState => false;
    internal bool AllowsCommands => false;

    internal static SceneDialogueModePolicy Instance { get; } = new SceneDialogueModePolicy();
}
