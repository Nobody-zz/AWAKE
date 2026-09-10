using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PersonaWorkbench.Core;

public static class AuthoringHandoffSharedFingerprint
{
    public static string Compute(AuthoringHandoffEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        string input = string.Join(
            "\n",
            envelope.SchemaVersion,
            envelope.HandoffId,
            envelope.WorkspaceId,
            envelope.DocumentId,
            envelope.Revision.ToString(CultureInfo.InvariantCulture),
            envelope.SharedContentSha256,
            envelope.Producer,
            envelope.ReviewOnly ? "true" : "false",
            envelope.SharedReviewStatus,
            FormatUtc(envelope.IssuedAtUtc),
            FormatUtc(envelope.ExpiresAtUtc),
            envelope.Payload);

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    public static string FormatUtc(DateTime value)
    {
        DateTime utc = value.Kind == DateTimeKind.Utc
            ? value
            : value.ToUniversalTime();
        return JsonSerializer.Serialize(utc).Trim('"');
    }
}
