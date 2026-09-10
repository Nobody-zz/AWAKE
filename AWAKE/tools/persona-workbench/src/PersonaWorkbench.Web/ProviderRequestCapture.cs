using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PersonaWorkbench.Web;

public sealed class ProviderRequestCapture : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly HashSet<string> AllowedStages = new HashSet<string>(StringComparer.Ordinal)
    {
        "expand-short",
        "convert-short",
        "expand-long",
        "convert-long"
    };
    private static readonly ConcurrentDictionary<string, object> FileLocks = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncLocal<ProviderRequestCaptureScope?> _currentScope = new AsyncLocal<ProviderRequestCaptureScope?>();
    private readonly string _path;
    private readonly object _disposeGate = new object();
    private bool _disposed;

    public ProviderRequestCapture(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Capture path is required.", nameof(path));
        _path = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
    }

    public static ProviderRequestCapture? FromEnvironment()
    {
        string? path = Environment.GetEnvironmentVariable("PWB_REQUEST_CAPTURE_PATH");
        return string.IsNullOrWhiteSpace(path) ? null : new ProviderRequestCapture(path);
    }

    public ProviderRequestCaptureScope BeginScope(string stage)
    {
        ThrowIfDisposed();
        if (!AllowedStages.Contains(stage)) throw new ArgumentException("Diagnostic stage is invalid.", nameof(stage));
        ProviderRequestCaptureScope scope = new ProviderRequestCaptureScope(this, stage, _currentScope.Value);
        _currentScope.Value = scope;
        return scope;
    }

    internal bool TryCaptureCurrent(string operation, string protocol, string endpointClass, string model, string inputText, byte[] bodyBytes)
    {
        if (_disposed || bodyBytes == null) return false;
        ProviderRequestCaptureScope? scope = _currentScope.Value;
        if (scope == null || !ReferenceEquals(scope.Owner, this)) return false;

        byte[] inputBytes = Encoding.UTF8.GetBytes(inputText ?? string.Empty);
        ProviderRequestCaptureRecord record = new ProviderRequestCaptureRecord
        {
            SchemaVersion = "persona-workbench.request-capture.v1",
            Stage = scope.Stage,
            Operation = operation,
            Protocol = protocol,
            EndpointClass = endpointClass,
            Model = model,
            Input = new ProviderRequestCaptureInput
            {
                Source = "workbench-provider-input",
                Bytes = inputBytes.Length,
                Sha256 = Sha256(inputBytes)
            },
            SafeHeaders = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ContentType"] = "application/json"
            },
            BodyBase64 = Convert.ToBase64String(bodyBytes),
            BodyBytes = bodyBytes.Length
        };

        string line = JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine;
        object fileLock = FileLocks.GetOrAdd(_path, static _ => new object());
        lock (fileLock)
        {
            try
            {
                ThrowIfDisposed();
                using FileStream stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
                byte[] lineBytes = Encoding.UTF8.GetBytes(line);
                stream.Write(lineBytes, 0, lineBytes.Length);
                stream.Flush(true);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    internal void RestoreScope(ProviderRequestCaptureScope scope)
    {
        if (ReferenceEquals(_currentScope.Value, scope)) _currentScope.Value = scope.Previous;
    }

    public void Dispose()
    {
        lock (_disposeGate) _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        lock (_disposeGate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProviderRequestCapture));
        }
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

public sealed class ProviderRequestCaptureScope : IDisposable
{
    private bool _disposed;

    internal ProviderRequestCaptureScope(ProviderRequestCapture owner, string stage, ProviderRequestCaptureScope? previous)
    {
        Owner = owner;
        Stage = stage;
        Previous = previous;
    }

    internal ProviderRequestCapture Owner { get; }
    internal string Stage { get; }
    internal ProviderRequestCaptureScope? Previous { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Owner.RestoreScope(this);
    }
}

public sealed class ProviderRequestCaptureRecord
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
    public string EndpointClass { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public ProviderRequestCaptureInput Input { get; init; } = new ProviderRequestCaptureInput();
    public Dictionary<string, string> SafeHeaders { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public string BodyBase64 { get; init; } = string.Empty;
    public int BodyBytes { get; init; }
}

public sealed class ProviderRequestCaptureInput
{
    public string Source { get; init; } = string.Empty;
    public int Bytes { get; init; }
    public string Sha256 { get; init; } = string.Empty;
}
