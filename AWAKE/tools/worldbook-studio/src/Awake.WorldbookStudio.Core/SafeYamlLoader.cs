using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Awake.WorldbookStudio.Core;

public sealed class SafeYamlLoader
{
    private readonly int _maxDepth;
    private readonly int _maxBytes;

    public SafeYamlLoader(int maxDepth = 64, int maxBytes = 2_000_000)
    {
        _maxDepth = maxDepth;
        _maxBytes = maxBytes;
    }

    public JsonObject Load(string path, ValidationReport report)
    {
        var bytes = File.ReadAllBytes(path);
        return Load(path, bytes, report);
    }

    public JsonObject Load(string path, byte[] bytes, ValidationReport report)
    {
        if (bytes.Length > _maxBytes)
        {
            report.Error("WB-YAML-002", "YAML 文件超过大小上限。", path);
            return new JsonObject();
        }

        try
        {
            var stream = new YamlStream();
            using var reader = new StringReader(System.Text.Encoding.UTF8.GetString(bytes));
            stream.Load(reader);
            if (stream.Documents.Count != 1)
            {
                report.Error("WB-YAML-003", "只允许一个 YAML 文档。", path);
                return new JsonObject();
            }

            var root = ConvertNode(stream.Documents[0].RootNode, 0, path, report);
            if (root is not JsonObject obj)
            {
                report.Error("WB-YAML-004", "根节点必须是映射。", path);
                return new JsonObject();
            }

            return obj;
        }
        catch (Exception ex) when (ex is YamlException or InvalidOperationException or FormatException)
        {
            report.Error("WB-YAML-001", "YAML 解析失败。", path, ex.Message);
            return new JsonObject();
        }
    }

    private JsonNode? ConvertNode(YamlNode node, int depth, string path, ValidationReport report)
    {
        if (depth > _maxDepth)
        {
            report.Error("WB-YAML-005", "YAML 嵌套深度超过上限。", path);
            return null;
        }

        var tag = node.Tag.ToString();
        if (node.GetType().Name.Equals("YamlAliasNode", StringComparison.Ordinal) || (node.Anchor.ToString() != "[empty]" && node.Anchor.ToString() != "") || (tag != "?" && tag != "" && !tag.StartsWith("tag:yaml.org,2002:", StringComparison.Ordinal)))
        {
            report.Error("WB-YAML-006", "禁止使用 YAML 锚点、别名和标签。", path);
            return null;
        }

        return node switch
        {
            YamlScalarNode scalar => ConvertScalar(scalar.Value, scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted),
            YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(x => ConvertNode(x, depth + 1, path, report)).ToArray()),
            YamlMappingNode mapping => ConvertMapping(mapping, depth, path, report),
            _ => null
        };
    }

    private JsonObject ConvertMapping(YamlMappingNode mapping, int depth, string path, ValidationReport report)
    {
        var result = new JsonObject();
        foreach (var pair in mapping.Children)
        {
            if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value))
            {
                report.Error("WB-YAML-007", "映射键必须是非空字符串。", path);
                continue;
            }

            if (result.ContainsKey(key.Value))
            {
                report.Error("WB-YAML-008", "禁止重复 YAML 键。", $"{path}.{key.Value}");
                continue;
            }

            result[key.Value] = ConvertNode(pair.Value, depth + 1, $"{path}.{key.Value}", report);
        }

        return result;
    }

    private static JsonNode? ConvertScalar(string? value, bool quoted = false)
    {
        if (value is null) return null;
        var text = value.Trim();
        if (text.Length == 0) return quoted ? JsonValue.Create(string.Empty) : null;
        if (text.Equals("null", StringComparison.OrdinalIgnoreCase) || text == "~") return null;
        if (bool.TryParse(text, out var boolean)) return JsonValue.Create(boolean);
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return JsonValue.Create(integer);
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue) && text.Contains('.')) return JsonValue.Create(decimalValue);
        return JsonValue.Create(value);
    }
}







