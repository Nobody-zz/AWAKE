using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Awake.WorldbookStudio.Core;

public sealed record StoredCloudProviderSettings(string BaseUrl, string Model, string ApiKey);

public static class ProviderSettingsStore
{
    // 云端与本机 Worker 共用同一份「本机加密配置」文件：两个 Save 都先读回旧值再整体重写，
    // 避免保存其中一半时把另一半清掉。
    private sealed record PersistedSettings(string BaseUrl, string Model, string EncryptedApiKey, string? LocalWorkerUrl, string? EncryptedLocalWorkerSecret);

    public static string DefaultPath()
    {
        var root = Environment.GetEnvironmentVariable("AWAKE_WB_PROVIDER_SETTINGS_PATH");
        if (!string.IsNullOrWhiteSpace(root)) return Path.GetFullPath(root);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local)) throw new InvalidOperationException("WB-AI-SETTINGS-500: 找不到本机配置目录。");
        return Path.Combine(local, "AWAKE", "WorldbookStudio", "provider-settings.json");
    }

    public static bool TryLoad(out StoredCloudProviderSettings settings, string? path = null)
    {
        settings = new StoredCloudProviderSettings(string.Empty, string.Empty, string.Empty);
        var persisted = Read(path);
        if (persisted is null || string.IsNullOrWhiteSpace(persisted.BaseUrl) || string.IsNullOrWhiteSpace(persisted.Model)) return false;
        if (!WindowsDpapiSecretProtector.TryUnprotect(persisted.EncryptedApiKey, out var apiKey)) return false;
        settings = new StoredCloudProviderSettings(persisted.BaseUrl, persisted.Model, apiKey);
        return true;
    }

    /// <summary>
    /// X1：本机 AI Worker 的地址与凭据此前只能靠环境变量提供，干净客户机上无处可填。
    /// 现在与云端 Key 走同一套本机加密保存，界面可直接配置。
    /// </summary>
    public static bool TryLoadLocalWorker(out string localWorkerUrl, out string localWorkerSecret, string? path = null)
    {
        localWorkerUrl = string.Empty;
        localWorkerSecret = string.Empty;
        var persisted = Read(path);
        if (persisted is null || string.IsNullOrWhiteSpace(persisted.LocalWorkerUrl)) return false;
        if (string.IsNullOrWhiteSpace(persisted.EncryptedLocalWorkerSecret)) return false;
        if (!WindowsDpapiSecretProtector.TryUnprotect(persisted.EncryptedLocalWorkerSecret, out var secret)) return false;
        localWorkerUrl = persisted.LocalWorkerUrl;
        localWorkerSecret = secret;
        return true;
    }

    public static void SaveLocalWorker(string localWorkerUrl, string localWorkerSecret, string? path = null)
    {
        if (!Uri.TryCreate(localWorkerUrl?.Trim(), UriKind.Absolute, out var uri)) throw new InvalidOperationException("WB-AI-SETTINGS-422: 本机 Worker 地址无效。");
        ProviderEndpointPolicy.ValidateLocalWorkerUri(uri);
        if (string.IsNullOrWhiteSpace(localWorkerSecret) || localWorkerSecret.Length > 4096 || localWorkerSecret.Any(char.IsControl)) throw new InvalidOperationException("WB-AI-SETTINGS-422: 本机 Worker 凭据无效。");
        if (!WindowsDpapiSecretProtector.TryProtect(localWorkerSecret, out var encrypted)) throw new InvalidOperationException("WB-AI-SETTINGS-500: 本机密钥保护不可用。");
        try
        {
            var existing = Read(path);
            Write(existing?.BaseUrl ?? string.Empty, existing?.Model ?? string.Empty, existing?.EncryptedApiKey ?? string.Empty, uri.AbsoluteUri, encrypted, path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(Encoding.UTF8.GetBytes(localWorkerSecret));
        }
    }

    public static void Save(string baseUrl, string model, string apiKey, string? path = null)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri)) throw new InvalidOperationException("WB-AI-SETTINGS-422: 云端地址无效。");
        ProviderEndpointPolicy.ValidateCloudBaseUri(uri);
        if (string.IsNullOrWhiteSpace(model) || model.Length > 256 || model.Any(char.IsControl)) throw new InvalidOperationException("WB-AI-SETTINGS-422: 模型名称无效。");
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length > 4096 || apiKey.Any(char.IsControl)) throw new InvalidOperationException("WB-AI-SETTINGS-422: API Key 无效。");
        if (!WindowsDpapiSecretProtector.TryProtect(apiKey, out var encrypted)) throw new InvalidOperationException("WB-AI-SETTINGS-500: 本机密钥保护不可用。");
        try
        {
            var existing = Read(path);
            Write(uri.AbsoluteUri, model.Trim(), encrypted, existing?.LocalWorkerUrl ?? string.Empty, existing?.EncryptedLocalWorkerSecret ?? string.Empty, path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(Encoding.UTF8.GetBytes(apiKey));
        }
    }

    private static PersistedSettings? Read(string? path)
    {
        var file = Path.GetFullPath(path ?? DefaultPath());
        if (!File.Exists(file)) return null;
        try
        {
            return JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(file));
        }
        catch (JsonException) { return null; }
        catch (CryptographicException) { return null; }
        catch (IOException) { return null; }
    }

    private static void Write(string baseUrl, string model, string encryptedApiKey, string localWorkerUrl, string encryptedLocalWorkerSecret, string? path)
    {
        var file = Path.GetFullPath(path ?? DefaultPath());
        var directory = Path.GetDirectoryName(file) ?? throw new InvalidOperationException("WB-AI-SETTINGS-500: 配置目录无效。");
        Directory.CreateDirectory(directory);
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var content = JsonSerializer.Serialize(new PersistedSettings(baseUrl, model, encryptedApiKey, localWorkerUrl, encryptedLocalWorkerSecret), new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }
            File.Move(temporary, file, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static void Delete(string? path = null)
    {
        var file = Path.GetFullPath(path ?? DefaultPath());
        if (File.Exists(file)) File.Delete(file);
    }

    public static void ClearLocalWorker(string? path = null)
    {
        var existing = Read(path);
        if (existing is null) return;
        if (string.IsNullOrWhiteSpace(existing.BaseUrl) || string.IsNullOrWhiteSpace(existing.EncryptedApiKey))
        {
            Delete(path);
            return;
        }
        Write(existing.BaseUrl, existing.Model, existing.EncryptedApiKey, string.Empty, string.Empty, path);
    }

    public static string Hint(string apiKey)
        => apiKey.Length <= 4 ? "已配置" : "••••" + apiKey[^4..];
}

internal static class WindowsDpapiSecretProtector
{
    private const int CryptProtectUiForbidden = 0x1;

    public static bool TryProtect(string plaintext, out string protectedValue)
    {
        protectedValue = string.Empty;
        if (!OperatingSystem.IsWindows()) return false;
        var inputBytes = Encoding.UTF8.GetBytes(plaintext);
        IntPtr inputBuffer = IntPtr.Zero;
        DataBlob output = default;
        try
        {
            inputBuffer = Marshal.AllocHGlobal(inputBytes.Length);
            Marshal.Copy(inputBytes, 0, inputBuffer, inputBytes.Length);
            var input = new DataBlob { Count = inputBytes.Length, Data = inputBuffer };
            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output)) return false;
            var outputBytes = new byte[output.Count];
            Marshal.Copy(output.Data, outputBytes, 0, outputBytes.Length);
            protectedValue = Convert.ToBase64String(outputBytes);
            CryptographicOperations.ZeroMemory(outputBytes);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(inputBytes);
            if (inputBuffer != IntPtr.Zero) Marshal.FreeHGlobal(inputBuffer);
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
    }

    public static bool TryUnprotect(string protectedValue, out string plaintext)
    {
        plaintext = string.Empty;
        if (!OperatingSystem.IsWindows()) return false;
        byte[] inputBytes;
        try { inputBytes = Convert.FromBase64String(protectedValue); }
        catch (FormatException) { return false; }
        IntPtr inputBuffer = IntPtr.Zero;
        IntPtr description = IntPtr.Zero;
        DataBlob output = default;
        try
        {
            inputBuffer = Marshal.AllocHGlobal(inputBytes.Length);
            Marshal.Copy(inputBytes, 0, inputBuffer, inputBytes.Length);
            var input = new DataBlob { Count = inputBytes.Length, Data = inputBuffer };
            if (!CryptUnprotectData(ref input, out description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output)) return false;
            var outputBytes = new byte[output.Count];
            Marshal.Copy(output.Data, outputBytes, 0, outputBytes.Length);
            plaintext = Encoding.UTF8.GetString(outputBytes);
            CryptographicOperations.ZeroMemory(outputBytes);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(inputBytes);
            if (inputBuffer != IntPtr.Zero) Marshal.FreeHGlobal(inputBuffer);
            if (description != IntPtr.Zero) LocalFree(description);
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Count;
        public IntPtr Data;
    }

    [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DataBlob input, out IntPtr description, IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("Kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
