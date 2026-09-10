using System;
using MarcusAwakeFramework.Api;

namespace Awake.SdkSmoke;

internal sealed class FakeClock
{
    internal FakeClock(DateTimeOffset? initialUtc = null)
    {
        UtcNow = initialUtc ?? DateTimeOffset.UtcNow;
    }

    internal DateTimeOffset UtcNow { get; private set; }

    internal RequestContext Context(
        string caller,
        SessionRef session = null,
        string correlationId = "fake-correlation",
        TimeSpan? budget = null)
    {
        return new RequestContext(
            new ExtensionId(caller),
            session ?? new SessionRef("fake-campaign", "fake-timeline", "fake-session"),
            correlationId,
            UtcNow.Add(budget ?? TimeSpan.FromSeconds(30)));
    }
}

internal static class MafAssertions
{
    internal static void Succeeded<T>(OperationResult<T> result, string message = null)
    {
        if (result == null || !result.IsSuccess)
        {
            throw new InvalidOperationException(message ?? "Expected a successful framework result.");
        }
    }

    internal static FrameworkError Failed<T>(
        OperationResult<T> result,
        FrameworkErrorCategory category,
        string code,
        string message = null)
    {
        if (result == null || result.IsSuccess || result.Error == null)
        {
            throw new InvalidOperationException(message ?? "Expected a failed framework result.");
        }
        if (result.Error.Category != category || !StringComparer.Ordinal.Equals(result.Error.Code, code))
        {
            throw new InvalidOperationException((message ?? "Framework error did not match.") + " Actual=" + result.Error.Code + "/" + result.Error.Category);
        }
        if (string.IsNullOrWhiteSpace(result.Error.Owner) || string.IsNullOrWhiteSpace(result.Error.CorrelationId))
        {
            throw new InvalidOperationException("Framework errors must retain owner and correlation ID.");
        }
        return result.Error;
    }

    internal static void Same<T>(T expected, T actual, string message = null)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException(message ?? ("Expected " + expected + " but received " + actual + "."));
        }
    }
}
