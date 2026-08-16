using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;

namespace Awake.SdkSmoke;

internal sealed class FakeKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

    internal int SetCount { get; private set; }
    internal bool FailSet { get; set; }
    internal int FailSetAfter { get; set; } = 1;
    internal bool FailGetWithKeyNotFound { get; set; }

    internal string GetValue(string key)
    {
        string value;
        if (_values.TryGetValue(key ?? string.Empty, out value)) return value;
        return null;
    }

    public Task<OperationResult<string>> GetAsync(string key, RequestContext context, CancellationToken cancellationToken)
    {
        if (FailGetWithKeyNotFound && string.IsNullOrEmpty(GetValue(key)))
        {
            return Task.FromResult(OperationResult<string>.Failed(FrameworkErrors.Create(
                "storage.key_not_found",
                FrameworkErrorCategory.NotFound,
                "key not found")));
        }
        return Task.FromResult(OperationResult<string>.Succeeded(GetValue(key)));
    }

    public Task<OperationResult<bool>> SetAsync(string key, string valueJson, RequestContext context, CancellationToken cancellationToken)
    {
        SetCount++;
        if (FailSet && SetCount >= FailSetAfter)
        {
            return Task.FromResult(OperationResult<bool>.Failed(FrameworkErrors.Create(
                "storage.write_failed",
                FrameworkErrorCategory.Unavailable,
                "write failed")));
        }
        _values[key ?? string.Empty] = valueJson;
        return Task.FromResult(OperationResult<bool>.Succeeded(true));
    }

    public Task<OperationResult<bool>> DeleteAsync(string key, RequestContext context, CancellationToken cancellationToken)
    {
        _values.Remove(key ?? string.Empty);
        return Task.FromResult(OperationResult<bool>.Succeeded(true));
    }
}
