using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MarcusAwakeTransport
{
    internal enum JsonNodeKind
    {
        Null,
        Boolean,
        Number,
        String,
        Array,
        Object
    }

    internal sealed class JsonNode
    {
        internal JsonNodeKind Kind;
        internal bool BooleanValue;
        internal string Scalar;
        internal List<JsonNode> Items;
        internal List<KeyValuePair<string, JsonNode>> Properties;
    }

    internal sealed class StrictJsonException : Exception
    {
        internal StrictJsonException(string message) : base(message) { }
    }

    internal static class StrictJson
    {
        internal static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        internal static JsonNode ParsePayload(string json) => Parse(json, ProtocolConstants.MaxPayloadBytes);
        internal static string RenderPayload(JsonNode node) => Render(node, ProtocolConstants.MaxPayloadBytes);
        internal static JsonNode Parse(string json) => Parse(json, ProtocolConstants.MaxFrameBytes);

        private static JsonNode Parse(string json, int maxBytes)
        {
            if (json == null) throw new StrictJsonException("json_missing");
            try
            {
                if (Utf8.GetByteCount(json) > maxBytes) throw new StrictJsonException("json_too_large");
            }
            catch (EncoderFallbackException)
            {
                throw new StrictJsonException("json_utf8_invalid");
            }
            return new Parser(json).Parse();
        }

        internal static string Render(JsonNode node) => Render(node, ProtocolConstants.MaxFrameBytes);

        private static string Render(JsonNode node, int maxBytes)
        {
            if (node == null) throw new StrictJsonException("node_missing");
            var builder = new StringBuilder();
            var propertyCount = 0;
            var arrayCount = 0;
            Write(node, builder, 0, ref propertyCount, ref arrayCount);
            try
            {
                if (Utf8.GetByteCount(builder.ToString()) > maxBytes) throw new StrictJsonException("json_too_large");
            }
            catch (EncoderFallbackException)
            {
                throw new StrictJsonException("json_utf8_invalid");
            }
            return builder.ToString();
        }

        internal static JsonNode Null() => new JsonNode { Kind = JsonNodeKind.Null };
        internal static JsonNode Boolean(bool value) => new JsonNode { Kind = JsonNodeKind.Boolean, BooleanValue = value };
        internal static JsonNode Number(long value) => new JsonNode { Kind = JsonNodeKind.Number, Scalar = value.ToString(CultureInfo.InvariantCulture) };
        internal static JsonNode Number(int value) => Number((long)value);
        internal static JsonNode String(string value) => new JsonNode { Kind = JsonNodeKind.String, Scalar = value ?? string.Empty };
        internal static JsonNode Array(IEnumerable<JsonNode> values) => new JsonNode { Kind = JsonNodeKind.Array, Items = values == null ? new List<JsonNode>() : values.ToList() };
        internal static JsonNode Object(IEnumerable<KeyValuePair<string, JsonNode>> properties) => new JsonNode { Kind = JsonNodeKind.Object, Properties = properties == null ? new List<KeyValuePair<string, JsonNode>>() : properties.ToList() };
        internal static KeyValuePair<string, JsonNode> Pair(string key, JsonNode value) => new KeyValuePair<string, JsonNode>(key, value ?? Null());

        internal static bool TryGet(JsonNode node, string name, out JsonNode value)
        {
            value = null;
            if (node == null || node.Kind != JsonNodeKind.Object) return false;
            for (var index = 0; index < node.Properties.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(node.Properties[index].Key, name))
                {
                    value = node.Properties[index].Value;
                    return true;
                }
            }
            return false;
        }

        internal static string StringValue(JsonNode node, string fallback = "") => node != null && node.Kind == JsonNodeKind.String ? node.Scalar ?? string.Empty : fallback;
        internal static bool BooleanValue(JsonNode node, bool fallback = false) => node != null && node.Kind == JsonNodeKind.Boolean ? node.BooleanValue : fallback;

        internal static long LongValue(JsonNode node, long fallback = 0)
        {
            long value;
            return node != null && node.Kind == JsonNodeKind.Number && long.TryParse(node.Scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        internal static string RequiredString(JsonNode node, string name)
        {
            JsonNode value;
            if (!TryGet(node, name, out value) || value.Kind != JsonNodeKind.String || string.IsNullOrWhiteSpace(value.Scalar)) throw new StrictJsonException("missing_" + name);
            return value.Scalar.Trim();
        }

        internal static int RequiredInt(JsonNode node, string name)
        {
            JsonNode value;
            if (!TryGet(node, name, out value) || value.Kind != JsonNodeKind.Number) throw new StrictJsonException("missing_" + name);
            var number = LongValue(value, long.MinValue);
            if (number == long.MinValue || number < int.MinValue || number > int.MaxValue) throw new StrictJsonException("invalid_" + name);
            return (int)number;
        }

        internal static long RequiredLong(JsonNode node, string name)
        {
            JsonNode value;
            if (!TryGet(node, name, out value) || value.Kind != JsonNodeKind.Number) throw new StrictJsonException("missing_" + name);
            var number = LongValue(value, long.MinValue);
            if (number == long.MinValue) throw new StrictJsonException("invalid_" + name);
            return number;
        }

        internal static string[] StringArray(JsonNode node, string name, bool required)
        {
            JsonNode value;
            if (!TryGet(node, name, out value))
            {
                if (required) throw new StrictJsonException("missing_" + name);
                return System.Array.Empty<string>();
            }
            if (value.Kind != JsonNodeKind.Array || value.Items.Count > ProtocolConstants.MaxJsonArrayItems) throw new StrictJsonException("invalid_" + name);
            var values = new string[value.Items.Count];
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < value.Items.Count; index++)
            {
                if (value.Items[index].Kind != JsonNodeKind.String || string.IsNullOrWhiteSpace(value.Items[index].Scalar)) throw new StrictJsonException("invalid_" + name);
                values[index] = value.Items[index].Scalar.Trim();
                if (!distinct.Add(values[index])) throw new StrictJsonException("duplicate_" + name);
            }
            return values;
        }

        internal static JsonNode RequiredNode(JsonNode node, string name)
        {
            JsonNode value;
            if (!TryGet(node, name, out value)) throw new StrictJsonException("missing_" + name);
            return value;
        }

        internal static string OptionalString(JsonNode node, string name)
        {
            JsonNode value;
            if (!TryGet(node, name, out value)) return string.Empty;
            if (value.Kind != JsonNodeKind.String) throw new StrictJsonException("invalid_" + name);
            return value.Scalar ?? string.Empty;
        }

        internal static long OptionalLong(JsonNode node, string name)
        {
            JsonNode value;
            if (!TryGet(node, name, out value)) return 0;
            if (value.Kind != JsonNodeKind.Number) throw new StrictJsonException("invalid_" + name);
            var parsed = LongValue(value, long.MinValue);
            if (parsed == long.MinValue) throw new StrictJsonException("invalid_" + name);
            return parsed;
        }

        internal static bool OptionalBoolean(JsonNode node, string name)
        {
            JsonNode value;
            if (!TryGet(node, name, out value)) return false;
            if (value.Kind != JsonNodeKind.Boolean) throw new StrictJsonException("invalid_" + name);
            return value.BooleanValue;
        }

        internal static bool RequiredBoolean(JsonNode node, string name)
        {
            JsonNode value;
            if (!TryGet(node, name, out value) || value.Kind != JsonNodeKind.Boolean) throw new StrictJsonException("missing_" + name);
            return value.BooleanValue;
        }

        internal static void EnsureAllowedProperties(JsonNode node, IEnumerable<string> allowedNames)
        {
            if (node == null || node.Kind != JsonNodeKind.Object) throw new StrictJsonException("json_object_required");
            var allowed = new HashSet<string>(allowedNames ?? System.Array.Empty<string>(), StringComparer.Ordinal);
            for (var index = 0; index < node.Properties.Count; index++)
            {
                if (!allowed.Contains(node.Properties[index].Key)) throw new StrictJsonException("unknown_" + node.Properties[index].Key);
            }
        }
        private static void Write(JsonNode node, StringBuilder builder, int depth, ref int propertyCount, ref int arrayCount)
        {
            if (node == null) throw new StrictJsonException("node_missing");
            if (depth > ProtocolConstants.MaxJsonDepth) throw new StrictJsonException("json_depth_exceeded");
            switch (node.Kind)
            {
                case JsonNodeKind.Null:
                    builder.Append("null");
                    return;
                case JsonNodeKind.Boolean:
                    builder.Append(node.BooleanValue ? "true" : "false");
                    return;
                case JsonNodeKind.Number:
                    if (string.IsNullOrWhiteSpace(node.Scalar)) throw new StrictJsonException("json_number_invalid");
                    builder.Append(node.Scalar);
                    return;
                case JsonNodeKind.String:
                    WriteString(node.Scalar ?? string.Empty, builder);
                    return;
                case JsonNodeKind.Array:
                    if (node.Items == null) throw new StrictJsonException("json_array_invalid");
                    arrayCount += node.Items.Count;
                    if (arrayCount > ProtocolConstants.MaxJsonArrayItems) throw new StrictJsonException("json_array_limit");
                    builder.Append('[');
                    for (var index = 0; index < node.Items.Count; index++)
                    {
                        if (index > 0) builder.Append(',');
                        Write(node.Items[index], builder, depth + 1, ref propertyCount, ref arrayCount);
                    }
                    builder.Append(']');
                    return;
                case JsonNodeKind.Object:
                    if (node.Properties == null) throw new StrictJsonException("json_object_invalid");
                    propertyCount += node.Properties.Count;
                    if (propertyCount > ProtocolConstants.MaxJsonProperties) throw new StrictJsonException("json_property_limit");
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    for (var index = 0; index < node.Properties.Count; index++)
                    {
                        if (!names.Add(node.Properties[index].Key ?? string.Empty)) throw new StrictJsonException("json_duplicate_key");
                    }
                    builder.Append('{');
                    var properties = node.Properties.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
                    for (var index = 0; index < properties.Length; index++)
                    {
                        if (index > 0) builder.Append(',');
                        WriteString(properties[index].Key, builder);
                        builder.Append(':');
                        Write(properties[index].Value, builder, depth + 1, ref propertyCount, ref arrayCount);
                    }
                    builder.Append('}');
                    return;
                default:
                    throw new StrictJsonException("unknown_node");
            }
        }

        private static void WriteString(string value, StringBuilder builder)
        {
            builder.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20) builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
        }

        private sealed class Parser
        {
            private readonly string json;
            private int position;
            private int propertyCount;
            private int arrayCount;

            internal Parser(string json) { this.json = json; }

            internal JsonNode Parse()
            {
                SkipWhitespace();
                var value = ParseValue(0);
                SkipWhitespace();
                if (position != json.Length) throw new StrictJsonException("trailing_json");
                return value;
            }

            private JsonNode ParseValue(int depth)
            {
                if (depth > ProtocolConstants.MaxJsonDepth) throw new StrictJsonException("json_depth_exceeded");
                SkipWhitespace();
                if (position >= json.Length) throw new StrictJsonException("json_unexpected_end");
                switch (json[position])
                {
                    case 'n': return ParseLiteral("null", Null());
                    case 't': return ParseLiteral("true", Boolean(true));
                    case 'f': return ParseLiteral("false", Boolean(false));
                    case '"': return String(ParseString());
                    case '[': return ParseArray(depth + 1);
                    case '{': return ParseObject(depth + 1);
                    default:
                        if (json[position] == '-' || char.IsDigit(json[position])) return ParseNumber();
                        throw new StrictJsonException("json_invalid_value");
                }
            }

            private JsonNode ParseLiteral(string literal, JsonNode value)
            {
                if (position + literal.Length > json.Length || !string.Equals(json.Substring(position, literal.Length), literal, StringComparison.Ordinal)) throw new StrictJsonException("json_invalid_literal");
                position += literal.Length;
                return value;
            }

            private JsonNode ParseObject(int depth)
            {
                position++;
                var values = new List<KeyValuePair<string, JsonNode>>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                SkipWhitespace();
                if (Consume('}')) return Object(values);
                while (true)
                {
                    SkipWhitespace();
                    if (position >= json.Length || json[position] != '"') throw new StrictJsonException("json_object_key_required");
                    var key = ParseString();
                    if (!names.Add(key)) throw new StrictJsonException("json_duplicate_key");
                    propertyCount++;
                    if (propertyCount > ProtocolConstants.MaxJsonProperties) throw new StrictJsonException("json_property_limit");
                    SkipWhitespace();
                    if (!Consume(':')) throw new StrictJsonException("json_object_colon_required");
                    values.Add(Pair(key, ParseValue(depth)));
                    SkipWhitespace();
                    if (Consume('}')) break;
                    if (!Consume(',')) throw new StrictJsonException("json_object_separator_required");
                }
                return Object(values);
            }

            private JsonNode ParseArray(int depth)
            {
                position++;
                var values = new List<JsonNode>();
                SkipWhitespace();
                if (Consume(']')) return Array(values);
                while (true)
                {
                    arrayCount++;
                    if (arrayCount > ProtocolConstants.MaxJsonArrayItems) throw new StrictJsonException("json_array_limit");
                    values.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (Consume(']')) break;
                    if (!Consume(',')) throw new StrictJsonException("json_array_separator_required");
                }
                return Array(values);
            }

            private JsonNode ParseNumber()
            {
                var start = position;
                if (json[position] == '-') position++;
                if (position >= json.Length) throw new StrictJsonException("json_number_invalid");
                if (json[position] == '0') position++;
                else
                {
                    if (!char.IsDigit(json[position]) || json[position] == '0') throw new StrictJsonException("json_number_invalid");
                    while (position < json.Length && char.IsDigit(json[position])) position++;
                }
                if (position < json.Length && json[position] == '.')
                {
                    position++;
                    if (position >= json.Length || !char.IsDigit(json[position])) throw new StrictJsonException("json_number_invalid");
                    while (position < json.Length && char.IsDigit(json[position])) position++;
                }
                if (position < json.Length && (json[position] == 'e' || json[position] == 'E'))
                {
                    position++;
                    if (position < json.Length && (json[position] == '+' || json[position] == '-')) position++;
                    if (position >= json.Length || !char.IsDigit(json[position])) throw new StrictJsonException("json_number_invalid");
                    while (position < json.Length && char.IsDigit(json[position])) position++;
                }
                var raw = json.Substring(start, position - start);
                double parsed;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) || double.IsNaN(parsed) || double.IsInfinity(parsed)) throw new StrictJsonException("json_number_invalid");
                return new JsonNode { Kind = JsonNodeKind.Number, Scalar = raw };
            }

            private string ParseString()
            {
                if (!Consume('"')) throw new StrictJsonException("json_string_required");
                var builder = new StringBuilder();
                while (position < json.Length)
                {
                    var character = json[position++];
                    if (character == '"') return builder.ToString();
                    if (character < 0x20) throw new StrictJsonException("json_control_character");
                    if (character != '\\')
                    {
                        if (char.IsSurrogate(character)) throw new StrictJsonException("json_surrogate_invalid");
                        builder.Append(character);
                        continue;
                    }
                    if (position >= json.Length) throw new StrictJsonException("json_escape_invalid");
                    var escaped = json[position++];
                    switch (escaped)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            var high = ParseUnicode();
                            if (char.IsHighSurrogate(high))
                            {
                                if (position + 6 > json.Length || json[position] != '\\' || json[position + 1] != 'u') throw new StrictJsonException("json_surrogate_invalid");
                                position += 2;
                                var low = ParseUnicode();
                                if (!char.IsLowSurrogate(low)) throw new StrictJsonException("json_surrogate_invalid");
                                builder.Append(high).Append(low);
                            }
                            else if (char.IsLowSurrogate(high)) throw new StrictJsonException("json_surrogate_invalid");
                            else builder.Append(high);
                            break;
                        default: throw new StrictJsonException("json_escape_invalid");
                    }
                }
                throw new StrictJsonException("json_string_unclosed");
            }

            private char ParseUnicode()
            {
                if (position + 4 > json.Length) throw new StrictJsonException("json_unicode_invalid");
                int value = 0;
                for (var index = 0; index < 4; index++)
                {
                    var character = json[position++];
                    int digit;
                    if (character >= '0' && character <= '9') digit = character - '0';
                    else if (character >= 'a' && character <= 'f') digit = character - 'a' + 10;
                    else if (character >= 'A' && character <= 'F') digit = character - 'A' + 10;
                    else throw new StrictJsonException("json_unicode_invalid");
                    value = (value << 4) | digit;
                }
                return (char)value;
            }

            private void SkipWhitespace()
            {
                while (position < json.Length && (json[position] == ' ' || json[position] == '\t' || json[position] == '\r' || json[position] == '\n')) position++;
            }

            private bool Consume(char expected)
            {
                if (position >= json.Length || json[position] != expected) return false;
                position++;
                return true;
            }
        }
    }
}
