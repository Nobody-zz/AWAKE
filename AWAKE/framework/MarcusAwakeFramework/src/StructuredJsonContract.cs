using MarcusAwakeTransport;

namespace MarcusAwakeFramework.Api
{
    internal static class StructuredJsonContract
    {
        internal const int MaximumBytes = 65536;
        internal const int MaximumDepth = 16;
        internal const int MaximumProperties = 256;

        internal static bool TryValidateObject(string json, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json)) return true;
            return StructuredJsonCanonicalizer.TryCanonicalizeObject(json, out _, out error);
        }
    }
}
