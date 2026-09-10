using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Web;

public sealed record WebRuntimeContext(
    StudioBootstrap Bootstrap,
    string PackageRoot,
    byte[] LaunchNonce,
    string WorkspaceRoot,
    string SchemaRoot);

public static class WebRuntimeBootstrap
{
    private const uint FileTypePipe = 0x0003;

    public static WebRuntimeContext Load()
    {
        try { return LoadCore(); }
        catch (Exception ex)
        {
            WebStartupLog.Write(ex);
            throw;
        }
    }

    private static WebRuntimeContext LoadCore()
    {
        var environment = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(item => (string)item.Key, item => item.Value?.ToString(), StringComparer.OrdinalIgnoreCase);

        if (string.Equals(environment.GetValueOrDefault("AWAKE_WB_DEV_MODE"), "1", StringComparison.Ordinal))
        {
            var workspace = environment.GetValueOrDefault("WORLD_BOOK_WORKSPACE") ?? Path.Combine(AppContext.BaseDirectory, "workspace");
            var schema = environment.GetValueOrDefault("WORLD_BOOK_SCHEMA_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "schemas");
            var package = AppContext.BaseDirectory;
            var bootstrap = new StudioBootstrap(StudioRuntimeConstants.ProtocolVersion, "dev", "dev", workspace, schema, package);
            return new WebRuntimeContext(bootstrap, package, [1, 2, 3], Path.GetFullPath(workspace), Path.GetFullPath(schema));
        }

        var webDirectory = StudioRuntimeHashing.NormalizePathForIdentity(AppContext.BaseDirectory);
        var packageRoot = Directory.GetParent(webDirectory)?.FullName is { } parent
            ? StudioRuntimeHashing.NormalizePathForIdentity(parent)
            : throw new InvalidOperationException("WB-BOOTSTRAP-001: 无法推导发行包根目录。");
        var handleText = environment.GetValueOrDefault(StudioRuntimeConstants.BootstrapHandleEnvironment);
        if (!ulong.TryParse(handleText, NumberStyles.None, CultureInfo.InvariantCulture, out var rawHandle) || rawHandle == 0)
            throw new InvalidOperationException("WB-BOOTSTRAP-004: bootstrap 管道句柄无效。");
        if (IntPtr.Size == 4 && rawHandle > uint.MaxValue)
            throw new InvalidOperationException("WB-BOOTSTRAP-004: bootstrap 管道句柄超出进程位宽。");

        var handle = new SafeFileHandle(new IntPtr(unchecked((long)rawHandle)), ownsHandle: true);
        if (GetFileType(handle.DangerousGetHandle()) != FileTypePipe || !GetNamedPipeInfo(handle.DangerousGetHandle(), out _, out _, out _, out _))
        {
            handle.Dispose();
            throw new InvalidOperationException("WB-BOOTSTRAP-004: bootstrap 句柄不是有效管道。");
        }

        using (handle)
        using (var stream = new FileStream(handle, FileAccess.Read, 4096, isAsync: false))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            var bootstrap = StudioBootstrapCodec.ReadFrameAsync(stream, timeout.Token).GetAwaiter().GetResult();
            StudioBootstrapValidation.ValidateAgainstEnvironment(bootstrap, environment, packageRoot);
            var nonce = Convert.FromHexString(bootstrap.LaunchNonce);
            if (nonce.Length < 32) throw new InvalidOperationException("WB-BOOTSTRAP-003: 启动 nonce 长度不足。");
            return new WebRuntimeContext(
                bootstrap,
                packageRoot,
                nonce,
                StudioRuntimeHashing.NormalizePathForIdentity(bootstrap.Workspace),
                StudioRuntimeHashing.NormalizePathForIdentity(bootstrap.SchemaRoot));
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr hFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeInfo(IntPtr hNamedPipe, out uint lpFlags, out uint lpOutBufferSize, out uint lpInBufferSize, out uint lpMaxInstances);
}

internal static class WebStartupLog
{
    public static void Write(Exception exception)
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local)) return;
            var directory = Path.Combine(local, "AWAKE", "WorldbookStudio", "logs");
            Directory.CreateDirectory(directory);
            var code = exception.Message.Split(':', 2)[0];
            var line = $"{DateTime.UtcNow:O}|{code}|{exception.GetType().Name}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(directory, $"web-{DateTime.UtcNow:yyyyMMdd}.log"), line);
        }
        catch { }
    }
}
