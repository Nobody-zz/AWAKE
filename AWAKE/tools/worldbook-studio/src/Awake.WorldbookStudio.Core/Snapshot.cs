using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public enum WorkspaceReadStage
{
    InitialRead,
    AssemblyVerification,
    OutputBoundary
}

public sealed record WorkspaceReadEvent(string Path, WorkspaceReadStage Stage);

public sealed class WorkspaceReadProbe
{
    private readonly ConcurrentDictionary<string, ReadCounter> _counts = new(StringComparer.OrdinalIgnoreCase);

    public Action<WorkspaceReadEvent>? OnRead { get; set; }
    public Action<WorkspaceReadStage>? BeforeStage { get; set; }
    public IReadOnlyList<SnapshotReadInventoryEntry> ReadInventory => SnapshotInventory();

    internal void NotifyBeforeStage(WorkspaceReadStage stage) => BeforeStage?.Invoke(stage);

    internal void RecordRead(string path, WorkspaceReadStage stage)
    {
        var normalized = Path.GetFullPath(path);
        var counter = _counts.GetOrAdd(normalized, static _ => new ReadCounter());
        counter.Increment(stage);
        OnRead?.Invoke(new WorkspaceReadEvent(normalized, stage));
    }

    internal IReadOnlyList<SnapshotReadInventoryEntry> SnapshotInventory()
        => _counts.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Value.ToEntry(x.Key))
            .ToList();

    private sealed class ReadCounter
    {
        private int _initial;
        private int _assembly;
        private int _output;

        public void Increment(WorkspaceReadStage stage)
        {
            switch (stage)
            {
                case WorkspaceReadStage.InitialRead: Interlocked.Increment(ref _initial); break;
                case WorkspaceReadStage.AssemblyVerification: Interlocked.Increment(ref _assembly); break;
                case WorkspaceReadStage.OutputBoundary: Interlocked.Increment(ref _output); break;
            }
        }

        public SnapshotReadInventoryEntry ToEntry(string path)
            => new(path, Volatile.Read(ref _initial) + Volatile.Read(ref _assembly) + Volatile.Read(ref _output), Volatile.Read(ref _initial), Volatile.Read(ref _assembly), Volatile.Read(ref _output));
    }
}

public sealed record SnapshotReadInventoryEntry(
    string Path,
    int Count,
    int InitialReads,
    int AssemblyReads,
    int OutputReads);

public sealed class SnapshotInputFile
{
    public required string Path { get; init; }
    public required byte[] Bytes { get; init; }
    public required string Sha256 { get; init; }
    public required string Category { get; init; }
    public object? Parsed { get; internal set; }
}

internal sealed class SnapshotInputChangedException : InvalidOperationException
{
    public SnapshotInputChangedException(string detail) : base($"WB-CAS-409: 世界书输入在快照期间发生变化。{detail}") { }
}

public sealed class SnapshotInputStore
{
    private readonly WorkspaceService _workspace;
    private readonly WorkspaceReadProbe? _probe;
    private readonly Dictionary<string, SnapshotInputFile> _files;

    private SnapshotInputStore(WorkspaceService workspace, WorkspaceReadProbe? probe, Dictionary<string, SnapshotInputFile> files, IReadOnlyList<string>? authoringPaths)
    {
        _workspace = workspace;
        _probe = probe;
        _files = files;
        AuthoringPaths = authoringPaths;
    }

    public IReadOnlyList<SnapshotInputFile> Files => _files.Values.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
    public IReadOnlyList<string>? AuthoringPaths { get; }
    public string Fingerprint => Hashing.Sha256Text(string.Join("|", Files.Select(x => $"{NormalizePath(x.Path)}:{x.Sha256}").OrderBy(x => x, StringComparer.Ordinal)));
    public IReadOnlyList<SnapshotReadInventoryEntry> ReadInventory => _probe?.SnapshotInventory() ?? [];
    public int DownstreamReadCount { get; private set; }

    public static SnapshotInputStore Capture(WorkspaceService workspace)
    {
        return Capture(workspace, null);
    }

    public static SnapshotInputStore Capture(WorkspaceService workspace, IEnumerable<string>? authoringPaths)
    {
        var normalizedAuthoringPaths = authoringPaths?.Select(path => Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(workspace.Root, path))).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var paths = workspace.EnumerateSnapshotInputPaths(normalizedAuthoringPaths);
        var store = new SnapshotInputStore(workspace, workspace.ReadProbe, new Dictionary<string, SnapshotInputFile>(StringComparer.OrdinalIgnoreCase), normalizedAuthoringPaths);
        foreach (var path in paths) store.Capture(path, WorkspaceReadStage.InitialRead);
        return store;
    }

    public byte[] GetBytes(string path)
    {
        var key = Path.GetFullPath(path);
        if (!_files.TryGetValue(key, out var file)) throw new SnapshotInputChangedException($"输入未纳入闭包：{path}");
        return file.Bytes;
    }

    public string GetText(string path) => Encoding.UTF8.GetString(GetBytes(path));

    public IEnumerable<string> GetLines(string path)
        => GetText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    public void SetParsed(string path, object parsed)
    {
        var key = Path.GetFullPath(path);
        if (_files.TryGetValue(key, out var file)) file.Parsed = parsed;
    }

    public bool VerifyCurrent(WorkspaceReadStage stage)
    {
        _probe?.NotifyBeforeStage(stage);
        var currentPaths = _workspace.EnumerateSnapshotInputPaths(AuthoringPaths).Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!currentPaths.SetEquals(_files.Keys)) return false;
        foreach (var path in currentPaths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path)) return false;
            var bytes = ReadDisk(path, stage);
            if (!string.Equals(Hashing.Sha256Bytes(bytes), _files[path].Sha256, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private void Capture(string path, WorkspaceReadStage stage)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new SnapshotInputChangedException($"输入不存在：{fullPath}");
        var bytes = ReadDisk(fullPath, stage);
        var category = _workspace.SnapshotInputCategory(fullPath);
        var file = new SnapshotInputFile
        {
            Path = fullPath,
            Bytes = bytes,
            Sha256 = Hashing.Sha256Bytes(bytes),
            Category = category,
            Parsed = TryParseJson(bytes) ?? JsonValue.Create(Encoding.UTF8.GetString(bytes))
        };
        _files[fullPath] = file;
    }

    private byte[] ReadDisk(string path, WorkspaceReadStage stage)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            _probe?.RecordRead(path, stage);
            return bytes;
        }
        catch (FileNotFoundException)
        {
            throw new SnapshotInputChangedException($"输入读取时消失：{path}");
        }
        catch (DirectoryNotFoundException)
        {
            throw new SnapshotInputChangedException($"输入目录读取时消失：{path}");
        }
    }

    private static JsonNode? TryParseJson(byte[] bytes)
    {
        try { return JsonNode.Parse(bytes); }
        catch (JsonException) { return null; }
    }

    private string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path);
        if (WorkspaceRootGuard.IsSameOrInside(full, _workspace.Root)) return "workspace/" + Path.GetRelativePath(_workspace.Root, full).Replace('\\', '/').ToLowerInvariant();
        if (WorkspaceRootGuard.IsSameOrInside(full, _workspace.SchemaRoot)) return "schema/" + Path.GetRelativePath(_workspace.SchemaRoot, full).Replace('\\', '/').ToLowerInvariant();
        return full.Replace('\\', '/').ToLowerInvariant();
    }
}
