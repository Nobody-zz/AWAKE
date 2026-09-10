using System.Windows.Forms;

namespace Awake.WorldbookStudio.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new LauncherForm());
    }
}
