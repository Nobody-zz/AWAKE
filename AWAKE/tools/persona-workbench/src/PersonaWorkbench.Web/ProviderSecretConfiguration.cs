using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PersonaWorkbench.Web;

public interface IProviderSecretProtector
{
    bool IsAvailable { get; }

    bool TryProtect(string plaintext, out string protectedValue, out string errorCode);

    bool TryUnprotect(string protectedValue, out string plaintext, out string errorCode);
}

public sealed class ProviderPersistentConfiguration
{
    public string Endpoint { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string EncryptedApiKey { get; init; } = string.Empty;
}

public sealed class ProviderSecretConfigurationService
{
    private readonly IProviderSecretProtector _protector;

    public ProviderSecretConfigurationService(IProviderSecretProtector protector)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public bool TryCreatePersistent(string? endpoint, string? model, string? apiKey, out ProviderPersistentConfiguration? configuration, out string errorCode)
    {
        configuration = null;
        errorCode = string.Empty;
        if (!_protector.IsAvailable)
        {
            errorCode = "provider.secret_protection_unavailable";
            return false;
        }

        if (!ProviderEndpointPolicy.TryValidate(endpoint, out Uri parsedEndpoint, out errorCode)
            || string.IsNullOrWhiteSpace(model) || !IsValidSecret(apiKey))
        {
            if (string.IsNullOrEmpty(errorCode)) errorCode = "provider.configuration_invalid";
            return false;
        }

        if (!_protector.TryProtect(apiKey!, out string encryptedApiKey, out errorCode))
        {
            return false;
        }

        configuration = new ProviderPersistentConfiguration
        {
            Endpoint = parsedEndpoint.AbsoluteUri,
            Model = model.Trim(),
            EncryptedApiKey = encryptedApiKey
        };
        return true;
    }

    public bool TryGetApiKey(ProviderPersistentConfiguration? configuration, out string apiKey, out string errorCode)
    {
        apiKey = string.Empty;
        errorCode = string.Empty;
        if (!_protector.IsAvailable)
        {
            errorCode = "provider.secret_protection_unavailable";
            return false;
        }

        if (configuration == null || !ProviderEndpointPolicy.TryValidate(configuration.Endpoint, out _, out errorCode)
            || string.IsNullOrWhiteSpace(configuration.Model) || string.IsNullOrWhiteSpace(configuration.EncryptedApiKey))
        {
            if (string.IsNullOrEmpty(errorCode)) errorCode = "provider.configuration_invalid";
            return false;
        }

        return _protector.TryUnprotect(configuration.EncryptedApiKey, out apiKey, out errorCode);
    }

    private static bool IsValidSecret(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length <= 4096 && value.All(character => !char.IsControl(character));
    }
}

public sealed class ProviderSessionKeyVault
{
    private char[]? _apiKey;

    public bool TrySet(string? apiKey, out string errorCode)
    {
        errorCode = string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length > 4096 || apiKey.Any(char.IsControl))
        {
            errorCode = "provider.session_key_invalid";
            return false;
        }

        Clear();
        _apiKey = apiKey.ToCharArray();
        return true;
    }

    public bool TryGet(out string apiKey)
    {
        apiKey = _apiKey == null ? string.Empty : new string(_apiKey);
        return _apiKey != null;
    }

    public void Clear()
    {
        if (_apiKey == null) return;
        Array.Clear(_apiKey, 0, _apiKey.Length);
        _apiKey = null;
    }
}

public static class ProviderSecretRedactor
{
    private static readonly Regex AuthorizationHeader = new Regex("(?i)(authorization\\s*:\\s*bearer\\s+)[^\\s,;]+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Redact(string? text, IEnumerable<string>? knownSecrets = null)
    {
        string redacted = text ?? string.Empty;
        if (knownSecrets != null)
        {
            foreach (string secret in knownSecrets.Where(value => !string.IsNullOrEmpty(value)).OrderByDescending(value => value.Length))
            {
                redacted = redacted.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
            }
        }

        return AuthorizationHeader.Replace(redacted, "$1[REDACTED]");
    }
}

public sealed class WindowsDpapiSecretProtector : IProviderSecretProtector
{
    private const int CryptProtectUiForbidden = 0x1;

    public bool IsAvailable => OperatingSystem.IsWindows();

    public bool TryProtect(string plaintext, out string protectedValue, out string errorCode)
    {
        protectedValue = string.Empty;
        errorCode = string.Empty;
        if (!IsAvailable)
        {
            errorCode = "provider.secret_protection_unavailable";
            return false;
        }

        byte[] inputBytes = Encoding.UTF8.GetBytes(plaintext ?? string.Empty);
        IntPtr inputBuffer = IntPtr.Zero;
        DataBlob output = default;
        try
        {
            inputBuffer = Marshal.AllocHGlobal(inputBytes.Length);
            Marshal.Copy(inputBytes, 0, inputBuffer, inputBytes.Length);
            DataBlob input = new DataBlob { Count = inputBytes.Length, Data = inputBuffer };
            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output))
            {
                errorCode = "provider.dpapi_protect_failed";
                return false;
            }

            byte[] outputBytes = new byte[output.Count];
            Marshal.Copy(output.Data, outputBytes, 0, outputBytes.Length);
            protectedValue = Convert.ToBase64String(outputBytes);
            CryptographicOperations.ZeroMemory(outputBytes);
            return true;
        }
        catch (CryptographicException)
        {
            errorCode = "provider.dpapi_protect_failed";
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(inputBytes);
            if (inputBuffer != IntPtr.Zero) Marshal.FreeHGlobal(inputBuffer);
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
    }

    public bool TryUnprotect(string protectedValue, out string plaintext, out string errorCode)
    {
        plaintext = string.Empty;
        errorCode = string.Empty;
        if (!IsAvailable)
        {
            errorCode = "provider.secret_protection_unavailable";
            return false;
        }

        byte[] inputBytes;
        try
        {
            inputBytes = Convert.FromBase64String(protectedValue ?? string.Empty);
        }
        catch (FormatException)
        {
            errorCode = "provider.secret_invalid";
            return false;
        }

        IntPtr inputBuffer = IntPtr.Zero;
        IntPtr description = IntPtr.Zero;
        DataBlob output = default;
        try
        {
            inputBuffer = Marshal.AllocHGlobal(inputBytes.Length);
            Marshal.Copy(inputBytes, 0, inputBuffer, inputBytes.Length);
            DataBlob input = new DataBlob { Count = inputBytes.Length, Data = inputBuffer };
            if (!CryptUnprotectData(ref input, out description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output))
            {
                errorCode = "provider.dpapi_unprotect_failed";
                return false;
            }

            byte[] outputBytes = new byte[output.Count];
            Marshal.Copy(output.Data, outputBytes, 0, outputBytes.Length);
            plaintext = Encoding.UTF8.GetString(outputBytes);
            CryptographicOperations.ZeroMemory(outputBytes);
            return true;
        }
        catch (CryptographicException)
        {
            errorCode = "provider.dpapi_unprotect_failed";
            return false;
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
