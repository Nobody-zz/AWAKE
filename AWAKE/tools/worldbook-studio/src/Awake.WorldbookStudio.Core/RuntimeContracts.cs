using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Awake.WorldbookStudio.Core;

public static class StudioRuntimeConstants
{
    public const string Product = "AWAKE.WorldbookStudio";
    public const int ProtocolVersion = 1;
    public const int Port = 5077;
    public const string PortEnvironment = "AWAKE_WB_PORT";
    public const string WorkspaceEnvironment = "AWAKE_WB_WORKSPACE";
    public const string SchemaEnvironment = "AWAKE_WB_SCHEMA_ROOT";
    public const string PackageEnvironment = "AWAKE_WB_PACKAGE_ROOT";
    public const string InstanceEnvironment = "AWAKE_WB_INSTANCE_ID";
    public const string BootstrapHandleEnvironment = "AWAKE_WB_BOOTSTRAP_HANDLE";
    public const string MutexEnvironment = "AWAKE_WB_MUTEX_NAME";
    public const string DefaultMutexName = @"Local\AWAKE.WorldbookStudio.Launcher";

    public static int ResolvePort()
    {
        var value = Environment.GetEnvironmentVariable(PortEnvironment);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && port is >= 1024 and <= 65535
            ? port
            : Port;
    }
}

public sealed record StudioBootstrap(
    int ProtocolVersion,
    string InstanceId,
    string LaunchNonce,
    string Workspace,
    string SchemaRoot,
    string PackageRoot);

public sealed record StudioHealth(
    bool Ok,
    string Product,
    int ProtocolVersion,
    string InstanceId,
    string LaunchNonceHash,
    string WorkspaceIdHash,
    int Port);

public static class StudioRuntimeHashing
{
    public static string Sha256Prefix(string value, int hexCharacters = 16)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..hexCharacters].ToLowerInvariant();
    }

    public static string NormalizePathForIdentity(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.StartsWith(@"\\?\", StringComparison.Ordinal)) full = full[4..];
        return full;
    }

    public static bool PathsEqual(string left, string right)
        => string.Equals(NormalizePathForIdentity(left), NormalizePathForIdentity(right), StringComparison.OrdinalIgnoreCase);

    public static string WorkspaceHash(string path)
        => Sha256Prefix(NormalizePathForIdentity(path));

    public static string PackageMutexName(string packageRoot)
        => $@"Local\AWAKE.WorldbookStudio.Launcher.{Sha256Prefix(NormalizePathForIdentity(packageRoot), 24)}";
}

public static class StudioBootstrapCodec
{
    public static byte[] EncodeFrame(StudioBootstrap bootstrap)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(bootstrap, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (json.Length is < 1 or > 65536) throw new InvalidOperationException("WB-BOOTSTRAP-001: 启动配置长度无效。");
        var frame = new byte[sizeof(uint) + json.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, sizeof(uint)), (uint)json.Length);
        json.CopyTo(frame.AsSpan(sizeof(uint)));
        return frame;
    }

    public static async Task<StudioBootstrap> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBytes = await ReadExactlyAsync(stream, sizeof(uint), cancellationToken).ConfigureAwait(false);
        var length = BitConverter.ToUInt32(lengthBytes, 0);
        if (length is < 1 or > 65536) throw new InvalidOperationException("WB-BOOTSTRAP-002: 启动配置长度无效。");
        var json = await ReadExactlyAsync(stream, checked((int)length), cancellationToken).ConfigureAwait(false);
        var bootstrap = JsonSerializer.Deserialize<StudioBootstrap>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (bootstrap is null
            || bootstrap.ProtocolVersion != StudioRuntimeConstants.ProtocolVersion
            || string.IsNullOrWhiteSpace(bootstrap.InstanceId)
            || string.IsNullOrWhiteSpace(bootstrap.LaunchNonce)
            || string.IsNullOrWhiteSpace(bootstrap.Workspace)
            || string.IsNullOrWhiteSpace(bootstrap.SchemaRoot)
            || string.IsNullOrWhiteSpace(bootstrap.PackageRoot))
        {
            throw new InvalidOperationException("WB-BOOTSTRAP-002: 启动配置内容无效。");
        }

        return bootstrap;
    }

    private static async Task<byte[]> ReadExactlyAsync(Stream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new InvalidOperationException("WB-BOOTSTRAP-002: 启动配置提前结束。");
            offset += read;
        }

        return buffer;
    }
}

public static class StudioBootstrapValidation
{
    public static void ValidateAgainstEnvironment(StudioBootstrap bootstrap, IReadOnlyDictionary<string, string?> environment, string packageDirectory)
    {
        var expectedPackage = StudioRuntimeHashing.NormalizePathForIdentity(packageDirectory);
        var actualPackage = RequiredPath(environment, StudioRuntimeConstants.PackageEnvironment);
        var actualWorkspace = RequiredPath(environment, StudioRuntimeConstants.WorkspaceEnvironment);
        var actualSchema = RequiredPath(environment, StudioRuntimeConstants.SchemaEnvironment);
        var actualInstance = Required(environment, StudioRuntimeConstants.InstanceEnvironment);

        if (!StudioRuntimeHashing.PathsEqual(bootstrap.PackageRoot, actualPackage)
            || !StudioRuntimeHashing.PathsEqual(actualPackage, expectedPackage)
            || !StudioRuntimeHashing.PathsEqual(bootstrap.Workspace, actualWorkspace)
            || !StudioRuntimeHashing.PathsEqual(bootstrap.SchemaRoot, actualSchema)
            || !string.Equals(bootstrap.InstanceId, actualInstance, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("WB-BOOTSTRAP-003: 启动配置与进程环境不一致。");
        }

        var schemaExpected = Path.Combine(expectedPackage, "schemas");
        if (!StudioRuntimeHashing.PathsEqual(bootstrap.SchemaRoot, schemaExpected))
            throw new InvalidOperationException("WB-BOOTSTRAP-003: 规范目录必须来自发行包。");

        WorkspacePathPolicy.ValidateWorkspaceRoot(bootstrap.Workspace, expectedPackage, bootstrap.SchemaRoot);
    }

    private static string Required(IReadOnlyDictionary<string, string?> environment, string name)
        => !environment.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"WB-BOOTSTRAP-001: 缺少 {name}。")
            : value;

    private static string RequiredPath(IReadOnlyDictionary<string, string?> environment, string name)
    {
        var value = Required(environment, name);
        if (!Path.IsPathFullyQualified(value)) throw new InvalidOperationException($"WB-BOOTSTRAP-001: {name} 必须是绝对路径。");
        return value;
    }
}

public static class WorkspacePathPolicy
{
    private static readonly string[] DefaultProtectedRoots =
    [
        @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord",
        @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE",
        @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\ModuleData",
        @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\PlayerExports",
        @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\PlayerExports"
    ];

    public static void ValidateWorkspaceRoot(string root, string? packageRoot = null, string? schemaRoot = null, IEnumerable<string>? immutableRoots = null)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("WB-ROOT-001: workspace 路径不能为空。");
        var full = StudioRuntimeHashing.NormalizePathForIdentity(root);
        var driveRoot = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(driveRoot) || string.Equals(full, driveRoot.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-ROOT-002: workspace 不能是磁盘根目录。");
        if (full.StartsWith(@"\\Device\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-ROOT-003: 不允许设备路径。");
        if (full.Contains(':', StringComparison.Ordinal) && Path.GetFileName(full).Contains(':', StringComparison.Ordinal))
            throw new InvalidOperationException("WB-ROOT-004: 不允许备用数据流路径。");
        if (File.Exists(full)) throw new InvalidOperationException("WB-ROOT-005: workspace 必须是目录。");

        var protectedRoots = new List<string>(DefaultProtectedRoots);
        if (!string.IsNullOrWhiteSpace(packageRoot)) protectedRoots.Add(packageRoot);
        if (!string.IsNullOrWhiteSpace(schemaRoot)) protectedRoots.Add(schemaRoot);
        if (immutableRoots is not null) protectedRoots.AddRange(immutableRoots.Where(x => !string.IsNullOrWhiteSpace(x))!);

        if (!string.IsNullOrWhiteSpace(schemaRoot))
        {
            var awakeRoot = Directory.GetParent(Path.GetFullPath(schemaRoot))?.Parent?.FullName;
            if (!string.IsNullOrWhiteSpace(awakeRoot))
            {
                protectedRoots.Add(Path.Combine(awakeRoot, "ModuleData"));
                protectedRoots.Add(Path.Combine(awakeRoot, "dist"));
                protectedRoots.Add(Path.Combine(awakeRoot, "_build_out"));
                protectedRoots.Add(Path.Combine(awakeRoot, "candidate_frozen"));
                protectedRoots.Add(Path.Combine(awakeRoot, "pending_game"));
            }
        }

        foreach (var candidate in protectedRoots)
        {
            var protectedPath = StudioRuntimeHashing.NormalizePathForIdentity(candidate);
            if (IsSameOrInside(full, protectedPath))
                throw new InvalidOperationException("WB-ROOT-001: workspace 命中受保护路径。");
        }

        foreach (var segment in SplitSegments(full))
        {
            if (segment.Equals("Modules", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("PlayerExports", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-ROOT-006: workspace 不能位于游戏模块目录。");
        }

        foreach (var ancestor in ExistingAncestors(full))
        {
            if (Path.GetFileName(ancestor).Equals("Mount & Blade II Bannerlord", StringComparison.OrdinalIgnoreCase)
                && HasBannerlordMarkers(ancestor))
                throw new InvalidOperationException("WB-ROOT-007: workspace 不能位于 Bannerlord 游戏目录。");
        }

        EnsureNoReparsePoint(full);
    }

    public static bool IsSameOrInside(string path, string root)
    {
        var normalizedPath = StudioRuntimeHashing.NormalizePathForIdentity(path);
        var normalizedRoot = StudioRuntimeHashing.NormalizePathForIdentity(root);
        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureNoReparsePoint(string path)
    {
        var current = StudioRuntimeHashing.NormalizePathForIdentity(path);
        while (!string.IsNullOrWhiteSpace(current) && Path.GetPathRoot(current) is { } root && current.Length >= root.Length)
        {
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("WB-ROOT-008: workspace 路径不能经过 junction 或 reparse point。");
            var parent = Directory.GetParent(current)?.FullName;
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent ?? string.Empty;
        }
    }

    private static IEnumerable<string> ExistingAncestors(string path)
    {
        var current = path;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current)) yield return current;
            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }
    }

    private static bool HasBannerlordMarkers(string root)
        => File.Exists(Path.Combine(root, "Modules", "Native", "SubModule.xml"))
            || File.Exists(Path.Combine(root, "Modules", "SandBoxCore", "SubModule.xml"))
            || Directory.Exists(Path.Combine(root, "bin", "Win64_Shipping_Client"));

    private static IEnumerable<string> SplitSegments(string path)
        => path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
}

public sealed record WorkspaceMarker(int Version, string Product, string CreatedAtUtc);

public sealed record StudioSettings(int Version, string Workspace, string Language);
