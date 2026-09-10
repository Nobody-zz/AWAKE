using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public static class ContractHashing
{
    public static string ManifestHash(JsonObject manifest)
    {
        var input = JsonNode.Parse(manifest.ToJsonString())!.AsObject();
        input.Remove("hashes");
        return HashCanonical(input);
    }

    public static string ContentHash(IEnumerable<(string Path, JsonNode Content)> files)
    {
        var index = new JsonObject();
        foreach (var file in files.OrderBy(x => NormalizePath(x.Path), StringComparer.OrdinalIgnoreCase))
        {
            index[NormalizePath(file.Path)] = CanonicalJson.Canonicalize(file.Content);
        }
        return HashCanonical(index);
    }

    public static string PackageHash(string manifestHash, string contentHash)
    {
        var manifestBytes = Convert.FromHexString(manifestHash);
        var contentBytes = Convert.FromHexString(contentHash);
        return Convert.ToHexString(SHA256.HashData(manifestBytes.Concat(contentBytes).ToArray()));
    }

    public static string HashCanonical(JsonNode node) => HashBytes(Encoding.UTF8.GetBytes(CanonicalJson.Serialize(node)));

    public static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');

}
