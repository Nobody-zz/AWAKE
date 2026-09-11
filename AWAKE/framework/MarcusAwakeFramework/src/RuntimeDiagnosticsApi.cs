using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api
{
    public sealed class RuntimeDiagnostic
    {
        public RuntimeDiagnostic(IReadOnlyDictionary<string, string> fields)
        {
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }

        public IReadOnlyDictionary<string, string> Fields { get; }
    }

    public static class RuntimeDiagnosticRedactor
    {
        public const string RedactedMarker = "[REDACTED]";

        private static readonly HashSet<string> OmittedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "raw_prompt", "raw_template", "raw_model_output", "api_key", "secret", "authorization_header", "raw_provider_payload", "raw_hero_id", "database_path", "filesystem_path", "endpoint_url"
        };

        private static readonly HashSet<string> MarkerFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "credential_reference", "identity_permission", "fine_grained_permissions"
        };

        public static RuntimeDiagnostic Redact(IReadOnlyDictionary<string, string> fields)
        {
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            var redacted = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in fields)
            {
                if (OmittedFields.Contains(pair.Key)) continue;
                redacted[pair.Key] = MarkerFields.Contains(pair.Key) ? RedactedMarker : pair.Value ?? string.Empty;
            }
            return new RuntimeDiagnostic(redacted);
        }
    }
}
