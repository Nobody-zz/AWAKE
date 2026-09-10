using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public sealed class AuthoringSaveFailureException : InvalidOperationException
{
    public AuthoringSaveFailureException(
        string code,
        int httpStatus,
        string message,
        IReadOnlyList<Diagnostic>? diagnostics = null,
        bool resultUnknown = false)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Diagnostics = diagnostics ?? [];
        ResultUnknown = resultUnknown;
    }

    public string Code { get; }
    public int HttpStatus { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }
    public bool ResultUnknown { get; }
}

public sealed record AdvancedSaveCheckResult(
    string Status,
    JsonObject? EditorDocument = null,
    AuthoringDocumentFile? Document = null,
    string? ExpectedContentHash = null);

internal sealed record AdvancedSaveResult(
    AuthoringDocumentFile Document,
    string ExpectedContentHash);
