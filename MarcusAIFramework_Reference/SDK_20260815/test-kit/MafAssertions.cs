using System;
using MarcusAIFramework.Api;

namespace MarcusAIFramework.Sdk.TestKit
{
    public static class MafAssertions
    {
        public static void Succeeded<T>(OperationResult<T> result, string message = null)
        {
            if (result == null || !result.IsSuccess) throw new InvalidOperationException(message ?? "Expected a successful framework result.");
        }

        public static FrameworkError Failed<T>(OperationResult<T> result, FrameworkErrorCategory category, string code, string message = null)
        {
            if (result == null || result.IsSuccess || result.Error == null)
                throw new InvalidOperationException(message ?? "Expected a failed framework result.");
            if (result.Error.Category != category || !StringComparer.Ordinal.Equals(result.Error.Code, code))
                throw new InvalidOperationException((message ?? "Framework error did not match.") + " Actual=" + result.Error.Code + "/" + result.Error.Category);
            if (string.IsNullOrWhiteSpace(result.Error.Owner) || string.IsNullOrWhiteSpace(result.Error.CorrelationId))
                throw new InvalidOperationException("Framework errors must retain owner and correlation ID.");
            return result.Error;
        }

        public static void Same<T>(T expected, T actual, string message = null)
        {
            if (!Equals(expected, actual)) throw new InvalidOperationException(message ?? ("Expected " + expected + " but received " + actual + "."));
        }
    }
}
