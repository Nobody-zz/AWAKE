using TaleWorlds.MountAndBlade;

namespace Awake;

internal static class SceneDialogueVisualCapabilities
{
    internal static bool ContourCapable { get; private set; } = true;

    internal static void Probe(Agent agent)
    {
        try
        {
            if (agent?.AgentVisuals == null) return;
            uint color = new TaleWorlds.Library.Color(1f, 0.84f, 0.2f, 1f).ToUnsignedInteger();
            agent.AgentVisuals.SetContourColor(color, true);
            agent.AgentVisuals.SetContourColor(null, true);
            ContourCapable = true;
        }
        catch
        {
            ContourCapable = false;
            AwakeLog.Write("scene_visual_contour_unavailable");
        }
    }

    internal static void ResetForTesting()
    {
        ContourCapable = true;
    }
}
