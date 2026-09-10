using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PersonaWorkbench.Core;

public sealed class PersonaWorkspaceException : Exception
{
    public PersonaWorkspaceException(string code, string message, string? conflictPath = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        ConflictPath = conflictPath;
    }

    public string Code { get; }
    public string? ConflictPath { get; }
}

public sealed class PersonaWorkspaceDocument
{
    public PersonaWorkspaceDocument(PersonaDocument document, string contentHash)
    {
        Document = document;
        ContentHash = contentHash;
    }

    public PersonaDocument Document { get; }
    public string ContentHash { get; }
}

public sealed class PersonaWorkspaceJournal
{
    public const string VersionValue = "persona-workbench.commit.v1";

    public string Version { get; set; } = VersionValue;
    public string TargetRelativePath { get; set; } = string.Empty;
    public string BackupRelativePath { get; set; } = string.Empty;
    public string TemporaryRelativePath { get; set; } = string.Empty;
    public string PreviousContentHash { get; set; } = string.Empty;
    public string PendingContentHash { get; set; } = string.Empty;
    public string Phase { get; set; } = "prepared";
}

public sealed class PersonaWorkspace : IDisposable
{
    private const string LockFileName = ".persona-workbench.lock";
    private const string JournalFileName = ".persona-workbench.commit.json";
    private static readonly JsonSerializerOptions JournalOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    private readonly FileStream _lockStream;
    private bool _disposed;

    private PersonaWorkspace(string rootPath, FileStream lockStream)
    {
        RootPath = rootPath;
        _lockStream = lockStream;
    }

    public string RootPath { get; }

    public static PersonaWorkspace Open(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Workspace path is required.", nameof(rootPath));
        string fullRoot = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(fullRoot);
        RejectReparsePoints(fullRoot, fullRoot);

        try
        {
            FileStream lockStream = new FileStream(
                Path.Combine(fullRoot, LockFileName),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.WriteThrough);
            return new PersonaWorkspace(fullRoot, lockStream);
        }
        catch (IOException ex)
        {
            throw new PersonaWorkspaceException("workspace.locked", "Workspace is already open for writing.", innerException: ex);
        }
    }

    public static string GetJournalPath(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Workspace path is required.", nameof(rootPath));
        return Path.Combine(Path.GetFullPath(rootPath), JournalFileName);
    }

    public PersonaWorkspaceDocument Read(string relativePath, PersonaTagRegistry registry)
    {
        ThrowIfDisposed();
        string fullPath = ResolvePath(relativePath);
        PersonaDocument document = PersonaDocumentStore.Read(fullPath, registry);
        return new PersonaWorkspaceDocument(document, ComputeContentHash(fullPath));
    }

    public PersonaWorkspaceDocument Save(string relativePath, PersonaDocument document, string? expectedContentHash)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(document);
        string targetPath = ResolvePath(relativePath);
        string? currentHash = File.Exists(targetPath) ? ComputeContentHash(targetPath) : null;
        if (!string.Equals(expectedContentHash, currentHash, StringComparison.Ordinal))
        {
            string conflictPath = SaveConflictDraft(relativePath, document);
            throw new PersonaWorkspaceException("workspace.conflict", "Workspace file changed outside the current session.", conflictPath);
        }

        string json = PersonaDocumentCodec.Serialize(document);
        string backupRelativePath = string.Empty;
        if (File.Exists(targetPath))
        {
            backupRelativePath = CreateHistoryBackup(relativePath, targetPath, currentHash!);
        }

        string temporaryRelativePath = Path.Combine(".pending", Guid.NewGuid().ToString("N") + ".tmp");
        string temporaryPath = ResolvePath(temporaryRelativePath);
        WriteTextDurably(temporaryPath, json);

        PersonaWorkspaceJournal journal = new PersonaWorkspaceJournal
        {
            TargetRelativePath = relativePath,
            BackupRelativePath = backupRelativePath,
            TemporaryRelativePath = temporaryRelativePath,
            PreviousContentHash = currentHash ?? string.Empty,
            PendingContentHash = ComputeContentHash(temporaryPath),
            Phase = "prepared"
        };
        WriteTextDurably(GetJournalPath(RootPath), JsonSerializer.Serialize(journal, JournalOptions));

        try
        {
            string? targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory)) Directory.CreateDirectory(targetDirectory);
            File.Move(temporaryPath, targetPath, overwrite: true);
            File.Delete(GetJournalPath(RootPath));
        }
        catch
        {
            throw;
        }

        return new PersonaWorkspaceDocument(document, ComputeContentHash(targetPath));
    }

    public void Recover()
    {
        ThrowIfDisposed();
        string journalPath = GetJournalPath(RootPath);
        if (!File.Exists(journalPath)) return;

        PersonaWorkspaceJournal journal;
        try
        {
            journal = JsonSerializer.Deserialize<PersonaWorkspaceJournal>(File.ReadAllText(journalPath, Encoding.UTF8), JournalOptions)
                ?? throw new PersonaWorkspaceException("workspace.journal_invalid", "Workspace commit journal is empty.");
        }
        catch (PersonaWorkspaceException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new PersonaWorkspaceException("workspace.journal_invalid", "Workspace commit journal is invalid.", innerException: ex);
        }

        if (!string.Equals(journal.Version, PersonaWorkspaceJournal.VersionValue, StringComparison.Ordinal)
            || !string.Equals(journal.Phase, "prepared", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(journal.TargetRelativePath)
            || string.IsNullOrWhiteSpace(journal.TemporaryRelativePath))
        {
            throw new PersonaWorkspaceException("workspace.journal_invalid", "Workspace commit journal is not supported.");
        }

        string targetPath = ResolvePath(journal.TargetRelativePath);
        string temporaryPath = ResolvePath(journal.TemporaryRelativePath);
        string backupPath = string.IsNullOrWhiteSpace(journal.BackupRelativePath)
            ? string.Empty
            : ResolvePath(journal.BackupRelativePath);

        bool hasHashes = !string.IsNullOrWhiteSpace(journal.PreviousContentHash)
            || !string.IsNullOrWhiteSpace(journal.PendingContentHash);
        if (hasHashes)
        {
            bool targetExists = File.Exists(targetPath);
            string targetHash = targetExists ? ComputeContentHash(targetPath) : string.Empty;
            bool pendingExists = File.Exists(temporaryPath);
            bool pendingMatches = pendingExists
                && !string.IsNullOrWhiteSpace(journal.PendingContentHash)
                && string.Equals(ComputeContentHash(temporaryPath), journal.PendingContentHash, StringComparison.Ordinal);
            bool targetIsPending = targetExists
                && !string.IsNullOrWhiteSpace(journal.PendingContentHash)
                && string.Equals(targetHash, journal.PendingContentHash, StringComparison.Ordinal);
            bool targetIsPrevious = targetExists
                && !string.IsNullOrWhiteSpace(journal.PreviousContentHash)
                && string.Equals(targetHash, journal.PreviousContentHash, StringComparison.Ordinal);
            bool backupMatches = File.Exists(backupPath)
                && !string.IsNullOrWhiteSpace(journal.PreviousContentHash)
                && string.Equals(ComputeContentHash(backupPath), journal.PreviousContentHash, StringComparison.Ordinal);

            if (targetIsPending)
            {
                CleanupRecoveredCommit(journalPath, temporaryPath);
                return;
            }
            if (pendingMatches && (!targetExists || targetIsPrevious))
            {
                string? targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory)) Directory.CreateDirectory(targetDirectory);
                File.Move(temporaryPath, targetPath, overwrite: true);
                File.Delete(journalPath);
                return;
            }
            if (!targetExists && backupMatches)
            {
                string? targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory)) Directory.CreateDirectory(targetDirectory);
                File.Copy(backupPath, targetPath, overwrite: true);
                CleanupRecoveredCommit(journalPath, temporaryPath);
                return;
            }

            throw new PersonaWorkspaceException(
                targetExists ? "workspace.recovery_conflict" : "workspace.recovery_unavailable",
                targetExists
                    ? "Workspace target changed during commit recovery."
                    : "Workspace recovery data is unavailable or does not match the commit journal.");
        }

        if (!File.Exists(targetPath))
        {
            if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath))
            {
                string? targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory)) Directory.CreateDirectory(targetDirectory);
                File.Copy(backupPath, targetPath, overwrite: true);
            }
            else if (File.Exists(temporaryPath))
            {
                File.Move(temporaryPath, targetPath, overwrite: true);
            }
            else
            {
                throw new PersonaWorkspaceException("workspace.recovery_unavailable", "Workspace recovery data is unavailable.");
            }
        }

        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        File.Delete(journalPath);
    }

    private static void CleanupRecoveredCommit(string journalPath, string temporaryPath)
    {
        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        if (File.Exists(journalPath)) File.Delete(journalPath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lockStream.Dispose();
    }

    private string CreateHistoryBackup(string relativePath, string targetPath, string contentHash)
    {
        string fileName = Path.GetFileName(relativePath);
        string historyRelativePath = Path.Combine(
            ".history",
            Path.GetFileNameWithoutExtension(fileName) + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "." + contentHash[..12] + Path.GetExtension(fileName));
        string historyPath = ResolvePath(historyRelativePath);
        string? directory = Path.GetDirectoryName(historyPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.Copy(targetPath, historyPath, overwrite: false);
        return historyRelativePath;
    }

    private string SaveConflictDraft(string relativePath, PersonaDocument document)
    {
        string fileName = Path.GetFileNameWithoutExtension(relativePath);
        string conflictRelativePath = Path.Combine(
            ".conflicts",
            fileName + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "." + Guid.NewGuid().ToString("N") + ".persona.json");
        string conflictPath = ResolvePath(conflictRelativePath);
        WriteTextDurably(conflictPath, PersonaDocumentCodec.Serialize(document));
        return conflictPath;
    }

    private string ResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) throw new PersonaWorkspaceException("workspace.path_invalid", "Workspace relative path is required.");
        if (Path.IsPathRooted(relativePath)) throw new PersonaWorkspaceException("workspace.path_outside", "Workspace path must be relative.");

        string fullPath = Path.GetFullPath(Path.Combine(RootPath, relativePath));
        string prefix = RootPath.EndsWith(Path.DirectorySeparatorChar) ? RootPath : RootPath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new PersonaWorkspaceException("workspace.path_outside", "Workspace path escapes the selected root.");
        }

        RejectReparsePoints(RootPath, fullPath);
        return fullPath;
    }

    private static void WriteTextDurably(string path, string content)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Target directory is required.");
        Directory.CreateDirectory(directory);

        using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        using StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true);
        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static string ComputeContentHash(string path)
    {
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void RejectReparsePoints(string rootPath, string candidatePath)
    {
        string root = Path.GetFullPath(rootPath);
        string candidate = Path.GetFullPath(candidatePath);
        string? current = Directory.Exists(candidate) ? candidate : Path.GetDirectoryName(candidate);
        while (!string.IsNullOrWhiteSpace(current) && current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new PersonaWorkspaceException("workspace.reparse_point", "Workspace paths cannot traverse reparse points.");
            }
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase)) break;
            current = Path.GetDirectoryName(current);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PersonaWorkspace));
    }
}
