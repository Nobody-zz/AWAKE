using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider;

public sealed class ApiKeyCredential : IDisposable
{
    private readonly object sync = new object();
    private char[] secret;
    private bool disposed;

    public ApiKeyCredential(string reference, string value)
    {
        Reference = ProviderContract.RequireIdentifier(reference, nameof(reference));
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Credential value is required.", nameof(value));
        secret = value.ToCharArray();
    }

    public string Reference { get; }
    public bool IsDisposed
    {
        get
        {
            lock (sync) return disposed;
        }
    }

    internal ApiKeyCredential(string reference, char[] ownedSecret)
    {
        Reference = ProviderContract.RequireIdentifier(reference, nameof(reference));
        if (ownedSecret == null || ownedSecret.Length == 0) throw new ArgumentException("Credential value is required.", nameof(ownedSecret));
        secret = ownedSecret.ToArray();
    }

    internal string CopySecretForRequest()
    {
        lock (sync)
        {
            ThrowIfDisposed();
            return new string(secret);
        }
    }

    internal char[] CopySecretChars()
    {
        lock (sync)
        {
            ThrowIfDisposed();
            return secret.ToArray();
        }
    }

    internal string GetSecretForTest()
    {
        return CopySecretForRequest();
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            Array.Clear(secret, 0, secret.Length);
            secret = Array.Empty<char>();
            disposed = true;
        }
    }

    public override string ToString()
    {
        return "ApiKeyCredential(reference=" + Reference + ",state=" + (IsDisposed ? "disposed" : "present") + ")";
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(ApiKeyCredential));
    }
}

public interface IProviderCredentialStore : IDisposable
{
    Task<ProviderResult<bool>> SaveAsync(ApiKeyCredential credential, CancellationToken cancellationToken = default);
    Task<ProviderResult<ApiKeyCredential>> GetAsync(string reference, CancellationToken cancellationToken = default);
    Task<ProviderResult<bool>> DeleteAsync(string reference, CancellationToken cancellationToken = default);
}

public sealed class InMemoryCredentialStore : IProviderCredentialStore
{
    private readonly object sync = new object();
    private readonly Dictionary<string, char[]> values = new Dictionary<string, char[]>(StringComparer.Ordinal);
    private bool disposed;

    public Task<ProviderResult<bool>> SaveAsync(ApiKeyCredential credential, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.Cancelled()));
        if (credential == null) throw new ArgumentNullException(nameof(credential));
        try
        {
            var copy = credential.CopySecretChars();
            lock (sync)
            {
                ThrowIfDisposed();
                if (values.TryGetValue(credential.Reference, out var previous)) Array.Clear(previous, 0, previous.Length);
                values[credential.Reference] = copy;
            }

            return Task.FromResult(ProviderResult<bool>.Succeeded(true));
        }
        catch (ObjectDisposedException exception) when (exception.ObjectName == nameof(ApiKeyCredential))
        {
            return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.Invalid("credential.disposed")));
        }
        catch (ObjectDisposedException)
        {
            return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.Internal("credential.store_disposed")));
        }
    }

    public Task<ProviderResult<ApiKeyCredential>> GetAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Cancelled()));
        var validReference = ProviderContract.RequireIdentifier(reference, nameof(reference));
        lock (sync)
        {
            try
            {
                ThrowIfDisposed();
                if (!values.TryGetValue(validReference, out var value)) return Task.FromResult(ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.NotFound(validReference)));
                return Task.FromResult(ProviderResult<ApiKeyCredential>.Succeeded(new ApiKeyCredential(validReference, value.ToArray())));
            }
            catch (ObjectDisposedException)
            {
                return Task.FromResult(ProviderResult<ApiKeyCredential>.Failed(CredentialErrors.Internal("credential.store_disposed")));
            }
        }
    }

    public Task<ProviderResult<bool>> DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.Cancelled()));
        var validReference = ProviderContract.RequireIdentifier(reference, nameof(reference));
        lock (sync)
        {
            try
            {
                ThrowIfDisposed();
                if (!values.Remove(validReference, out var value)) return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.NotFound(validReference)));
                Array.Clear(value, 0, value.Length);
                return Task.FromResult(ProviderResult<bool>.Succeeded(true));
            }
            catch (ObjectDisposedException)
            {
                return Task.FromResult(ProviderResult<bool>.Failed(CredentialErrors.Internal("credential.store_disposed")));
            }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            foreach (var value in values.Values) Array.Clear(value, 0, value.Length);
            values.Clear();
            disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(InMemoryCredentialStore));
    }
}

internal static class CredentialErrors
{
    internal static ProviderError Cancelled() => new ProviderError("credential.cancelled", ProviderErrorCategory.Cancelled, "Credential operation cancelled.", false, "credentials");
    internal static ProviderError Invalid(string code) => new ProviderError(code, ProviderErrorCategory.InvalidRequest, "Credential operation rejected.", false, "credentials");
    internal static ProviderError NotFound(string reference) => new ProviderError("credential.not_found", ProviderErrorCategory.NotFound, "Credential was not found.", false, "credentials", details: new Dictionary<string, string> { ["reference"] = reference });
    internal static ProviderError Internal(string code) => new ProviderError(code, ProviderErrorCategory.InternalFailure, "Credential storage is unavailable.", false, "credentials");
    internal static ProviderError Io(string code) => new ProviderError(code, ProviderErrorCategory.TransportUnavailable, "Credential storage is unavailable.", true, "credentials");
    internal static ProviderError Corrupt() => new ProviderError("credential.corrupt", ProviderErrorCategory.CorruptCredential, "Credential record is corrupt.", false, "credentials");
    internal static ProviderError Unsupported() => new ProviderError("credential.protection_unsupported", ProviderErrorCategory.Unsupported, "Credential protection is unavailable.", false, "credentials");
}
