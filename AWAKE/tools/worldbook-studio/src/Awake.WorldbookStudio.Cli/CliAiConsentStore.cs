using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Cli;

public sealed record CliAiConsentRecord(
    string SchemaVersion,
    string TokenHash,
    DateTimeOffset ExpiresAt,
    string DocumentPathHash,
    string ProviderId,
    string Analysis,
    string DocumentId,
    string SourceDocumentHash,
    int SavedRevision,
    string RequestHash,
    string BufferId);

public sealed class CliAiConsentStore
{
    private const string SchemaVersion = "ai-consent.v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    private readonly string _root;

    public CliAiConsentStore()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local)) throw new InvalidOperationException("WB-AI-CONSENT-500: 无法定位当前用户应用数据目录。");
        _root = Path.Combine(local, "AWAKE", "WorldbookStudio", "consent");
        Directory.CreateDirectory(_root);
    }

    public (string Token, CliAiConsentRecord Record) Create(AssistanceRequest request, string bufferId)
    {
        var token = NewToken(32);
        var record = new CliAiConsentRecord(
            SchemaVersion,
            Hashing.Sha256Text(token),
            DateTimeOffset.UtcNow.Add(Lifetime),
            Hashing.Sha256Text(request.DocumentPath),
            request.ProviderId,
            AssistanceAnalysisNames.ToWire(request.Analysis),
            request.DocumentId,
            request.SourceDocumentHash,
            request.SavedRevision,
            request.RequestHash,
            bufferId);
        var path = Path.Combine(_root, record.TokenHash + ".json");
        WriteAtomic(path, record);
        ProtectAndVerify(path);
        return (token, record);
    }

    public CliAiConsentRecord Consume(string token, string documentPath)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(documentPath)) throw NotFound();
        var tokenHash = Hashing.Sha256Text(token);
        var path = Path.Combine(_root, tokenHash + ".json");
        if (!File.Exists(path)) throw NotFound();
        CliAiConsentRecord record;
        ProtectAndVerify(path);
        try { record = Parse(File.ReadAllText(path)); }
        catch (Exception) { throw NotFound(); }
        if (record.ExpiresAt <= DateTimeOffset.UtcNow || !string.Equals(record.TokenHash, tokenHash, StringComparison.Ordinal) || !string.Equals(record.DocumentPathHash, Hashing.Sha256Text(documentPath), StringComparison.Ordinal))
            throw NotFound();
        try { File.Delete(path); }
        catch { throw new InvalidOperationException("WB-AI-CONSENT-500: 同意记录无法安全消费。"); }
        return record;
    }

    private void WriteAtomic(string path, CliAiConsentRecord record)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var node = JsonSerializer.SerializeToNode(record, JsonOptions)!.AsObject();
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = Encoding.UTF8.GetBytes(CanonicalJson.Serialize(node) + Environment.NewLine);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static CliAiConsentRecord Parse(string text)
    {
        var record = JsonSerializer.Deserialize<CliAiConsentRecord>(text, JsonOptions);
        if (record is null || record.SchemaVersion != SchemaVersion) throw new InvalidOperationException();
        return record;
    }

    private static void ProtectAndVerify(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("WB-AI-CONSENT-403: CLI 同意记录只支持 Windows 当前用户 ACL。");
        var identity = $"{Environment.UserDomainName}\\{Environment.UserName}";
        var result = RunIcacls($"\"{path}\" /inheritance:r /grant:r \"{identity}:R\"");
        if (result.ExitCode != 0) throw new InvalidOperationException("WB-AI-CONSENT-403: 无法设置 CLI 同意记录 ACL。");
        var verification = RunIcacls($"\"{path}\"");
        if (verification.ExitCode != 0 || !verification.Output.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase) || verification.Output.Contains("Everyone", StringComparison.OrdinalIgnoreCase) || verification.Output.Contains("BUILTIN\\Users", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-CONSENT-403: CLI 同意记录 ACL 验证失败。");
    }

    private static (int ExitCode, string Output) RunIcacls(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "icacls.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("WB-AI-CONSENT-403: icacls 不可用。");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static string NewToken(int bytes)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static InvalidOperationException NotFound()
        => new("WB-AI-CONSENT-404: AI 同意会话不存在或已失效。");
}