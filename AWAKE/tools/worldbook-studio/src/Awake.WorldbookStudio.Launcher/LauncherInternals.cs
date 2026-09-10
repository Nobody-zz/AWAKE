using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Awake.WorldbookStudio.Launcher.Tests")]

namespace Awake.WorldbookStudio.Launcher;

internal sealed class LauncherFailure : InvalidOperationException
{
    private LauncherFailure(string code, string stage, string safeMessage, Exception? innerException)
        : base($"{code}: {safeMessage}", innerException)
    {
        Code = code;
        Stage = stage;
        SafeMessage = safeMessage;
    }

    public string Code { get; }
    public string Stage { get; }
    public string SafeMessage { get; }

    public static LauncherFailure Create(string code, string stage, string safeMessage, Exception? innerException = null)
        => new(code, stage, safeMessage, innerException);

    public static LauncherFailure From(Exception exception, string stage)
    {
        if (exception is LauncherFailure failure) return failure;
        var message = exception.Message ?? string.Empty;
        var separator = message.IndexOf(':');
        var code = separator > 0 ? message[..separator].Trim() : "WB-START-001";
        var safeMessage = separator > 0 && separator + 1 < message.Length ? message[(separator + 1)..].Trim() : "Worldbook Studio 启动失败。";
        return new LauncherFailure(code, stage, safeMessage, exception);
    }
}

internal sealed class SmokeBrowserOpener : IBrowserOpener
{
    private readonly ILauncherEnvironment _environment;

    public SmokeBrowserOpener(ILauncherEnvironment environment) => _environment = environment;

    public bool Open(string address)
    {
        var mode = _environment.Get("AWAKE_WB_SMOKE_BROWSER");
        if (string.Equals(mode, "failure", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(mode, "real", StringComparison.OrdinalIgnoreCase)) return new ShellBrowserOpener().Open(address);
        return true;
    }
}
