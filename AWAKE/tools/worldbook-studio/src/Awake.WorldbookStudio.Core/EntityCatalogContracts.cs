using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed class EntityCatalogSnapshot
{
    public static EntityCatalogSnapshot Unavailable(IReadOnlyList<Diagnostic> diagnostics)
        => new(false, null, new JsonArray(), new JsonObject(), diagnostics);

    public EntityCatalogSnapshot(
        bool available,
        string? buildId,
        JsonArray entities,
        JsonObject counts,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        Available = available;
        BuildId = buildId;
        Entities = entities;
        Counts = counts;
        Diagnostics = diagnostics;
    }

    public bool Available { get; }
    public string? BuildId { get; }
    public JsonArray Entities { get; }
    public JsonObject Counts { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public JsonObject ToCatalogJson()
    {
        var diagnostics = new JsonArray(Diagnostics.Select(x => new JsonObject
        {
            ["severity"] = x.Severity,
            ["message"] = x.Message
        }).ToArray());

        return new JsonObject
        {
            ["available"] = Available,
            ["entities"] = Entities.DeepClone(),
            ["counts"] = Counts.DeepClone(),
            ["diagnostics"] = diagnostics
        };
    }
}
