using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider;

public sealed class ProtectedFileCredentialStore : IProviderCredentialStore
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MAWECRED");
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private readonly string directory;
    private readonly ICredentialProtector protector;
    private bool disposed;

    public ProtectedFileCredentialStore(string directory, CredentialProtectionMode mode = CredentialProtectionMode.PlatformPreferred)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Credential directory is required.", nameof(directory));
        this.directory = Path.GetFullPath(directory);
        protector = CredentialProtectorFactory.Create(mode);
    }

    public CredentialProtectionStatus ProtectionStatus => protector.Status;
    public string Warning => protector.Warning;

    public async Task<ProviderResult<bool>> SaveAsync(ApiKeyCredential credential, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return ProviderResult<bool>.Failed(CredentialErrors.Cancelled());
        if (credential == null) throw new ArgumentNullException(nameof(credential));
        if (disposed) return ProviderResult<bool>.Failed(CredentialErrors.Internal("credential.store_disposed"));
        if (protector.Status == CredentialProtectionStatus.Unsupported) return ProviderResult<bool>.Failed(CredentialErrors.Unsupported());

        char[] characters;
        try
        {
            characters = credential.CopySecretChars();
        }
        catch (ObjectDisposedException)
        {
            return ProviderResult<bool>.Failed(CredentialErrors.Invalid("credential.disposed"));
        }

        byte[] plaintext = Array.Empty<byte>();
        byte[] ciphertext = Array.Empty<byte>();
        byte[] envelope = Array.Empty<byte>();
        string temporaryPath = string.Empty;
        try
        {
            plaintext = Encoding.UTF8.GetBytes(characters);
            ciphertext = protector.Protect(plaintext);
            envelope = BuildEnvelope(protector.AlgorithmId, ciphertext);
            Directory.CreateDirectory(directory);
            var path = GetPath(credential.Reference);
            temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, envelope, cancellationToken).ConfigureAwait(false);
            using (var file = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Flush(true);
            }

            File.Move(temporaryPath, path, true);
            temporaryPath = string.Empty;
            ApplyPrivateMode(path);
            return ProviderResult<bool>.Succeeded(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ProviderResult<bool>.Failed(CredentialErrors.Cancelled());
        }
        catch (PlatformNotSupportedException)
        {
            return ProviderResult<bool>.Failed(CredentialErrors.Unsupported());
        }
        catch (CryptographicException)
        {
            return ProviderResult<bool>.Failed(CredentialErrors.Unsupported());
        }
        catch (IOException)
        {
            return ProviderResult<bool>.Failed(CredentialErrors.Io("credential.write_failed"));
        }
        catch (UnauthorizedAccessException)
        {
            return ProviderResult<bool>.Failed(CredentialErrors.Io("credential.write_denied"));
        }
        finally
        {
            Array.Clear(characters, 0, characters.Length);
            Array.Clear(plaintext, 0, plaintext.Length);
            Array.Clear(ciphertext, 0, ciphertext.Length);
            Array.Clear(envelope, 0, envelope.Length);
            if (!string.IsNullOrEmpty(temporaryPath)) TryDelete(temporaryPath);
        }
    }

    public async Task<ProviderResult<ApiKeyCredential>> GetAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Cancelled());
        var validReference = ProviderContract.RequireIdentifier(reference, nameof(reference));
        if (disposed) return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Internal("credential.store_disposed"));
        if (protector.Status == CredentialProtectionStatus.Unsupported) return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Unsupported());

        var path = GetPath(validReference);
        if (!File.Exists(path)) return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.NotFound(validReference));

        byte[] envelope = Array.Empty<byte>();
        byte[] ciphertext = Array.Empty<byte>();
        byte[] plaintext = Array.Empty<byte>();
        char[] characters = Array.Empty<char>();
        try
        {
            var info = new FileInfo(path);
            if (info.Length < Magic.Length + 3 || info.Length > 1_048_576) return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Corrupt());
            envelope = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var parsed = ParseEnvelope(envelope);
            if (parsed.AlgorithmId != protector.AlgorithmId) return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Unsupported());
            ciphertext = parsed.Ciphertext;
            plaintext = protector.Unprotect(ciphertext);
            characters = StrictUtf8.GetChars(plaintext);
            if (characters.Length == 0 || Array.IndexOf(characters, '\0') >= 0) return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Corrupt());
            return ProviderResult<ApiKeyCredential>.Succeeded(new ApiKeyCredential(validReference, characters));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Cancelled());
        }
        catch (PlatformNotSupportedException)
        {
            return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Unsupported());
        }
        catch (CryptographicException)
        {
            return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Corrupt());
        }
        catch (DecoderFallbackException)
        {
            return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Corrupt());
        }
        catch (InvalidDataException)
        {
            return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Corrupt());
        }
        catch (IOException)
        {
            return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Io("credential.read_failed"));
        }
        catch (UnauthorizedAccessException)
        {
            return ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Io("credential.read_denied"));
        }
        finally
        {
            Array.Clear(characters, 0, characters.Length);
            Array.Clear(plaintext, 0, plaintext.Length);
            Array.Clear(ciphertext, 0, ciphertext.Length);
            Array.Clear(envelope, 0, envelope.Length);
        }
    }
    public Task<ProviderResult<bool>> DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.Cancelled()));
        var validReference = ProviderContract.RequireIdentifier(reference, nameof(reference));
        if (disposed) return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.Internal("credential.store_disposed")));
        var path = GetPath(validReference);
        try
        {
            if (!File.Exists(path)) return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.NotFound(validReference)));
            File.Delete(path);
            return Task.FromResult(ProviderResult<bool>.Succeeded(true));
        }
        catch (IOException)
        {
            return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.Io("credential.delete_failed")));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.Io("credential.delete_denied")));
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        protector.Dispose();
    }

    private string GetPath(string reference)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reference))).ToLowerInvariant();
        return Path.Combine(directory, hash + ".credential");
    }

    private static byte[] BuildEnvelope(byte algorithmId, byte[] ciphertext)
    {
        var envelope = new byte[Magic.Length + 2 + ciphertext.Length];
        Buffer.BlockCopy(Magic, 0, envelope, 0, Magic.Length);
        envelope[Magic.Length] = 1;
        envelope[Magic.Length + 1] = algorithmId;
        Buffer.BlockCopy(ciphertext, 0, envelope, Magic.Length + 2, ciphertext.Length);
        return envelope;
    }

    private static (byte AlgorithmId, byte[] Ciphertext) ParseEnvelope(byte[] envelope)
    {
        if (envelope.Length < Magic.Length + 3) throw new InvalidDataException();
        for (var index = 0; index < Magic.Length; index++)
        {
            if (envelope[index] != Magic[index]) throw new InvalidDataException();
        }

        if (envelope[Magic.Length] != 1) throw new InvalidDataException();
        var ciphertext = new byte[envelope.Length - Magic.Length - 2];
        Buffer.BlockCopy(envelope, Magic.Length + 2, ciphertext, 0, ciphertext.Length);
        return (envelope[Magic.Length + 1], ciphertext);
    }

    private static void ApplyPrivateMode(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (IOException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal interface ICredentialProtector : IDisposable
{
    byte AlgorithmId { get; }
    CredentialProtectionStatus Status { get; }
    string Warning { get; }
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] ciphertext);
}

internal static class CredentialProtectorFactory
{
    internal static ICredentialProtector Create(CredentialProtectionMode mode)
    {
        if (mode == CredentialProtectionMode.AesGcm) return new AesGcmCredentialProtector();
        if (mode == CredentialProtectionMode.Dpapi) return OperatingSystem.IsWindows() ? new DpapiCredentialProtector() : new UnsupportedCredentialProtector();
        return OperatingSystem.IsWindows() ? new DpapiCredentialProtector() : new AesGcmCredentialProtector();
    }
}

internal sealed class AesGcmCredentialProtector : ICredentialProtector
{
    private readonly byte[] key;

    internal AesGcmCredentialProtector()
    {
        var material = Encoding.UTF8.GetBytes("marcus-awake-provider-credential-v1|" + Environment.UserDomainName + "|" + Environment.UserName + "|" + Environment.MachineName);
        key = SHA256.HashData(material);
        Array.Clear(material, 0, material.Length);
    }

    public byte AlgorithmId => 0xA1;
    public CredentialProtectionStatus Status => CredentialProtectionStatus.DegradedAesGcm;
    public string Warning => "AES-GCM credential storage is a degraded fallback; a same-user local process may recover the key.";

    public byte[] Protect(byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var ciphertext = new byte[plaintext.Length];
        try
        {
            using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plaintext, ciphertext, tag);
            var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
            return result;
        }
        finally
        {
            Array.Clear(nonce, 0, nonce.Length);
            Array.Clear(tag, 0, tag.Length);
            Array.Clear(ciphertext, 0, ciphertext.Length);
        }
    }

    public byte[] Unprotect(byte[] ciphertext)
    {
        if (ciphertext.Length < 28) throw new CryptographicException();
        var nonce = new byte[12];
        var tag = new byte[16];
        var plaintext = new byte[ciphertext.Length - nonce.Length - tag.Length];
        try
        {
            Buffer.BlockCopy(ciphertext, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(ciphertext, nonce.Length, tag, 0, tag.Length);
            using (var aes = new AesGcm(key, 16)) aes.Decrypt(nonce, ciphertext.AsSpan(nonce.Length + tag.Length), tag, plaintext);
            return plaintext;
        }
        catch
        {
            Array.Clear(plaintext, 0, plaintext.Length);
            throw;
        }
        finally
        {
            Array.Clear(nonce, 0, nonce.Length);
            Array.Clear(tag, 0, tag.Length);
        }
    }
    public void Dispose()
    {
        Array.Clear(key, 0, key.Length);
    }
}

internal sealed class UnsupportedCredentialProtector : ICredentialProtector
{
    public byte AlgorithmId => 0;
    public CredentialProtectionStatus Status => CredentialProtectionStatus.Unsupported;
    public string Warning => string.Empty;
    public byte[] Protect(byte[] plaintext) => throw new PlatformNotSupportedException();
    public byte[] Unprotect(byte[] ciphertext) => throw new PlatformNotSupportedException();
    public void Dispose() { }
}
