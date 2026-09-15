using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Awake.WorldbookRuntimeProductionSmoke;

internal static partial class Program
{
    private static Task TestDeveloperNegotiationEntryAsync()
    {
        MethodInfo launcherOverload = typeof(NpcDialogueLauncher).GetMethod(
            "TryOpenDialogue",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(AwakeNpcTarget), typeof(string), typeof(NpcDialogueActionMode) },
            null);
        Check(launcherOverload != null,
            "developer negotiation entry must expose the action-mode launcher overload");

        MethodInfo developerAction = typeof(AwakeDeveloperTestActions).GetMethod(
            "TestNearbyNegotiation",
            BindingFlags.Static | BindingFlags.NonPublic);
        Check(developerAction != null,
            "developer mode must expose the nearby negotiation test action");

        string workspaceRoot = FindWorkspaceRoot();
        string terminalSource = File.ReadAllText(
            Path.Combine(workspaceRoot, "src", "AwakeTerminalBehavior.cs"));
        Check(terminalSource.IndexOf("test_negotiation", StringComparison.Ordinal) >= 0,
            "developer menu must register the negotiation test item");
        Check(terminalSource.IndexOf("TestNearbyNegotiation()", StringComparison.Ordinal) >= 0,
            "developer menu must route the negotiation item to its action");

        return Task.CompletedTask;
    }

    private static string FindWorkspaceRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "AwakeTerminalBehavior.cs")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("unable to locate AWAKE source root for developer-mode fixture");
    }
}
