using System;

namespace MarcusAwakeRuntimeService
{
    /// <summary>
    /// Raw image bytes returned by the provider route, before the runtime imports them into the
    /// content-addressed asset store. Bytes never travel back to the game process inside a provider
    /// frame: the frame budget is 256 KB and a portrait is far larger than that.
    /// </summary>
    internal sealed class ProviderImageOutcome
    {
        internal ProviderImageOutcome(byte[] content, string mediaType, string resolvedModel)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            MediaType = mediaType ?? string.Empty;
            ResolvedModel = resolvedModel ?? string.Empty;
        }

        internal byte[] Content { get; }

        internal string MediaType { get; }

        internal string ResolvedModel { get; }
    }

    /// <summary>
    /// Unary result of the image route. Mirrors <c>ProviderStreamBeginResult</c>: exactly one of
    /// <see cref="Value"/> and <see cref="Error"/> is populated.
    /// </summary>
    internal sealed class ProviderImageCallResult
    {
        private ProviderImageCallResult(ProviderImageOutcome? value, ProviderWireError? error)
        {
            Value = value;
            Error = error;
        }

        internal ProviderImageOutcome? Value { get; }

        internal ProviderWireError? Error { get; }

        internal bool IsSuccess => Error == null;

        internal static ProviderImageCallResult Success(ProviderImageOutcome value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return new ProviderImageCallResult(value, null);
        }

        internal static ProviderImageCallResult Failure(ProviderWireError error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            return new ProviderImageCallResult(null, error);
        }
    }
}
