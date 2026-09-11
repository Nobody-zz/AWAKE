using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MarcusAwakeFramework.Api
{
    public sealed class CanonicalizationResult
    {
        public CanonicalizationResult(string domain, string canonicalJson, string hash)
        {
            Domain = ContractGuard.Id(domain, nameof(domain));
            CanonicalJson = canonicalJson ?? throw new ArgumentNullException(nameof(canonicalJson));
            Hash = ContractGuard.Id(hash, nameof(hash));
        }

        public string Domain { get; }
        public string CanonicalJson { get; }
        public string Hash { get; }
    }

    public sealed class TaskRequestCanonicalInput
    {
        public TaskRequestCanonicalInput(
            string routeId,
            string providerId,
            string profileId,
            string message,
            string inputJson,
            SchemaRef outputSchema,
            string settlementRequirement,
            int inputByteBudget,
            int outputByteBudget,
            int tokenBudget,
            int deltaBudget)
        {
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            Message = message ?? string.Empty;
            InputJson = inputJson ?? "null";
            OutputSchema = outputSchema ?? throw new ArgumentNullException(nameof(outputSchema));
            SettlementRequirement = ContractGuard.Id(settlementRequirement, nameof(settlementRequirement));
            InputByteBudget = inputByteBudget;
            OutputByteBudget = outputByteBudget;
            TokenBudget = tokenBudget;
            DeltaBudget = deltaBudget;
            if (inputByteBudget < 0 || outputByteBudget < 0 || tokenBudget < 0 || deltaBudget < 0) throw new ArgumentOutOfRangeException(nameof(inputByteBudget));
        }

        public string RouteId { get; }
        public string ProviderId { get; }
        public string ProfileId { get; }
        public string Message { get; }
        public string InputJson { get; }
        public SchemaRef OutputSchema { get; }
        public string SettlementRequirement { get; }
        public int InputByteBudget { get; }
        public int OutputByteBudget { get; }
        public int TokenBudget { get; }
        public int DeltaBudget { get; }
    }

    public sealed class EgressCanonicalInput
    {
        public EgressCanonicalInput(string routeId, string providerId, string profileId, string inputJson, IReadOnlyList<string> allowedFieldIds, IReadOnlyList<string> grantRuleIds, IReadOnlyList<string> archiveIds, IReadOnlyList<string> entryIds, IReadOnlyList<string> allowedDomains)
        {
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            InputJson = inputJson ?? "null";
            AllowedFieldIds = allowedFieldIds ?? new string[0];
            GrantRuleIds = grantRuleIds ?? new string[0];
            ArchiveIds = archiveIds ?? new string[0];
            EntryIds = entryIds ?? new string[0];
            AllowedDomains = allowedDomains ?? new string[0];
        }

        public string RouteId { get; }
        public string ProviderId { get; }
        public string ProfileId { get; }
        public string InputJson { get; }
        public IReadOnlyList<string> AllowedFieldIds { get; }
        public IReadOnlyList<string> GrantRuleIds { get; }
        public IReadOnlyList<string> ArchiveIds { get; }
        public IReadOnlyList<string> EntryIds { get; }
        public IReadOnlyList<string> AllowedDomains { get; }
    }

    public static class TaskRequestCanonicalizer
    {
        public const string TaskDomain = "marcus-awake/task-request/v1";
        public const string EgressDomain = "marcus-awake/egress/v1";

        private static readonly HashSet<string> SetLikePropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "allowed_field_ids", "grant_rule_ids", "archive_ids", "entry_ids", "allowed_domains"
        };

        public static OperationResult<CanonicalizationResult> ForTask(TaskRequestCanonicalInput input, string correlationId = "task-canonicalize")
        {
            if (input == null) return Failure("task.canonical_input_missing", "A task canonical input is required.", correlationId);
            var normalizedInput = CanonicalizeJson(input.InputJson, TaskDomain, correlationId: correlationId);
            if (!normalizedInput.IsSuccess) return normalizedInput;
            string json = "{" +
                "\"delta_budget\":" + input.DeltaBudget.ToString(CultureInfo.InvariantCulture) + "," +
                "\"input\":" + normalizedInput.Value.CanonicalJson + "," +
                "\"input_byte_budget\":" + input.InputByteBudget.ToString(CultureInfo.InvariantCulture) + "," +
                "\"message\":" + Quote(input.Message) + "," +
                "\"output_byte_budget\":" + input.OutputByteBudget.ToString(CultureInfo.InvariantCulture) + "," +
                "\"output_schema\":{\"id\":" + Quote(input.OutputSchema.SchemaId) + ",\"version\":{\"major\":" + input.OutputSchema.Version.Major.ToString(CultureInfo.InvariantCulture) + ",\"minor\":" + input.OutputSchema.Version.Minor.ToString(CultureInfo.InvariantCulture) + "}}," +
                "\"profile_id\":" + Quote(input.ProfileId) + "," +
                "\"provider_id\":" + Quote(input.ProviderId) + "," +
                "\"route_id\":" + Quote(input.RouteId) + "," +
                "\"settlement_requirement\":" + Quote(input.SettlementRequirement) + "," +
                "\"token_budget\":" + input.TokenBudget.ToString(CultureInfo.InvariantCulture) + "}";
            return CanonicalizeJson(json, TaskDomain, null, correlationId);
        }

        public static OperationResult<CanonicalizationResult> ForEgress(EgressCanonicalInput input, string correlationId = "egress-canonicalize")
        {
            if (input == null) return Failure("egress.canonical_input_missing", "An egress canonical input is required.", correlationId);
            var normalizedInput = CanonicalizeJson(input.InputJson, EgressDomain, correlationId: correlationId);
            if (!normalizedInput.IsSuccess) return normalizedInput;
            string json = "{" +
                "\"allowed_domains\":" + StringArray(input.AllowedDomains) + "," +
                "\"allowed_field_ids\":" + StringArray(input.AllowedFieldIds) + "," +
                "\"archive_ids\":" + StringArray(input.ArchiveIds) + "," +
                "\"entry_ids\":" + StringArray(input.EntryIds) + "," +
                "\"grant_rule_ids\":" + StringArray(input.GrantRuleIds) + "," +
                "\"input\":" + normalizedInput.Value.CanonicalJson + "," +
                "\"profile_id\":" + Quote(input.ProfileId) + "," +
                "\"provider_id\":" + Quote(input.ProviderId) + "," +
                "\"route_id\":" + Quote(input.RouteId) + "}";
            return CanonicalizeJson(json, EgressDomain, SetLikePropertyNames, correlationId);
        }

        public static OperationResult<CanonicalizationResult> CanonicalizeJson(string json, string domain, IReadOnlyCollection<string> setLikePropertyNames = null, string correlationId = "json-canonicalize")
        {
            if (string.IsNullOrWhiteSpace(json)) return Failure("canonical_json_missing", "Canonical JSON is required.", correlationId);
            if (string.IsNullOrWhiteSpace(domain)) return Failure("canonical_domain_missing", "Canonicalization domain is required.", correlationId);
            try
            {
                var parser = new Parser(json);
                JsonValue value = parser.Parse();
                string canonical = value.Write(setLikePropertyNames ?? new string[0]);
                string hash = Hash(domain, canonical);
                return OperationResult<CanonicalizationResult>.Succeeded(new CanonicalizationResult(domain, canonical, hash));
            }
            catch (CanonicalizationException error)
            {
                return Failure(error.Code, error.Message, correlationId);
            }
            catch (Exception error)
            {
                return Failure("canonical_json_invalid", "The JSON could not be canonicalized: " + error.Message, correlationId);
            }
        }

        public static string Hash(string domain, string canonicalJson)
        {
            string input = ContractGuard.Id(domain, nameof(domain)) + "\n" + (canonicalJson ?? string.Empty);
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                for (var index = 0; index < digest.Length; index++) builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static OperationResult<CanonicalizationResult> Failure(string code, string fallback, string correlationId)
        {
            return OperationResult<CanonicalizationResult>.Failed(FrameworkErrors.Create(code, FrameworkErrorCategory.InvalidRequest, fallback, correlationId));
        }

        private static string StringArray(IReadOnlyList<string> values)
        {
            var builder = new StringBuilder("[");
            for (var index = 0; index < values.Count; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append(Quote(values[index] ?? string.Empty));
            }
            return builder.Append(']').ToString();
        }

        private static string Quote(string value)
        {
            string normalized = Normalize(value ?? string.Empty);
            var builder = new StringBuilder(normalized.Length + 2).Append('"');
            for (var index = 0; index < normalized.Length; index++)
            {
                char character = normalized[index];
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\n"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20) builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(character);
                        break;
                }
            }
            return builder.Append('"').ToString();
        }

        private static string Normalize(string value)
        {
            return value.Replace("\r\n", "\n").Replace('\r', '\n').Normalize(NormalizationForm.FormC);
        }

        private enum JsonKind { Null, Boolean, Number, String, Array, Object }

        private sealed class JsonValue
        {
            internal JsonKind Kind;
            internal bool Boolean;
            internal string Scalar;
            internal List<JsonValue> Items;
            internal List<KeyValuePair<string, JsonValue>> Properties;

            internal string Write(IReadOnlyCollection<string> setLikeNames)
            {
                switch (Kind)
                {
                    case JsonKind.Null: return "null";
                    case JsonKind.Boolean: return Boolean ? "true" : "false";
                    case JsonKind.Number: return Scalar;
                    case JsonKind.String: return Quote(Scalar);
                    case JsonKind.Array:
                        var values = Items.Select(item => item.Write(setLikeNames)).ToList();
                        return "[" + string.Join(",", values) + "]";
                    case JsonKind.Object:
                        var properties = Properties.OrderBy(pair => Normalize(pair.Key), StringComparer.Ordinal).ToList();
                        var builder = new StringBuilder("{");
                        for (var index = 0; index < properties.Count; index++)
                        {
                            if (index > 0) builder.Append(',');
                            builder.Append(Quote(properties[index].Key)).Append(':');
                            JsonValue property = properties[index].Value;
                            if (property.Kind == JsonKind.Array && setLikeNames.Contains(properties[index].Key))
                            {
                                var items = property.Items.Select(item => item.Write(setLikeNames)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal);
                                builder.Append('[').Append(string.Join(",", items)).Append(']');
                            }
                            else builder.Append(property.Write(setLikeNames));
                        }
                        return builder.Append('}').ToString();
                    default: throw new CanonicalizationException("canonical_json_invalid", "Unknown JSON value kind.");
                }
            }
        }

        private sealed class Parser
        {
            private readonly string text;
            private int index;

            internal Parser(string text) { this.text = text; }

            internal JsonValue Parse()
            {
                SkipWhitespace();
                JsonValue value = ParseValue();
                SkipWhitespace();
                if (index != text.Length) throw Error("canonical_json_trailing_data", "Trailing data is not allowed.");
                return value;
            }

            private JsonValue ParseValue()
            {
                SkipWhitespace();
                if (index >= text.Length) throw Error("canonical_json_unexpected_end", "A JSON value is incomplete.");
                char character = text[index];
                if (character == 'n') return ParseLiteral("null", new JsonValue { Kind = JsonKind.Null });
                if (character == 't') return ParseLiteral("true", new JsonValue { Kind = JsonKind.Boolean, Boolean = true });
                if (character == 'f') return ParseLiteral("false", new JsonValue { Kind = JsonKind.Boolean, Boolean = false });
                if (character == '"') return new JsonValue { Kind = JsonKind.String, Scalar = ParseString() };
                if (character == '[') return ParseArray();
                if (character == '{') return ParseObject();
                if (character == '-' || (character >= '0' && character <= '9')) return ParseNumber();
                throw Error("canonical_json_invalid", "Unexpected JSON token at position " + index + ".");
            }

            private JsonValue ParseLiteral(string literal, JsonValue value)
            {
                if (index + literal.Length > text.Length || !StringComparer.Ordinal.Equals(text.Substring(index, literal.Length), literal)) throw Error("canonical_json_invalid", "Invalid JSON literal.");
                index += literal.Length;
                return value;
            }

            private JsonValue ParseObject()
            {
                index++;
                var values = new List<KeyValuePair<string, JsonValue>>();
                var keys = new HashSet<string>(StringComparer.Ordinal);
                SkipWhitespace();
                if (Consume('}')) return new JsonValue { Kind = JsonKind.Object, Properties = values };
                while (true)
                {
                    SkipWhitespace();
                    if (index >= text.Length) throw Error("canonical_json_unexpected_end", "An object is incomplete.");
                    if (text[index] != '"') throw Error("canonical_json_invalid", "Object keys must be strings.");
                    string key = Normalize(ParseString());
                    if (!keys.Add(key)) throw Error("canonical_json_duplicate_key", "Duplicate JSON object keys are not allowed.");
                    SkipWhitespace();
                    Require(':');
                    JsonValue value = ParseValue();
                    values.Add(new KeyValuePair<string, JsonValue>(key, value));
                    SkipWhitespace();
                    if (Consume('}')) break;
                    Require(',');
                }
                return new JsonValue { Kind = JsonKind.Object, Properties = values };
            }

            private JsonValue ParseArray()
            {
                index++;
                var values = new List<JsonValue>();
                SkipWhitespace();
                if (Consume(']')) return new JsonValue { Kind = JsonKind.Array, Items = values };
                while (true)
                {
                    if (index >= text.Length) throw Error("canonical_json_unexpected_end", "An array is incomplete.");
                    values.Add(ParseValue());
                    SkipWhitespace();
                    if (Consume(']')) break;
                    Require(',');
                }
                return new JsonValue { Kind = JsonKind.Array, Items = values };
            }

            private JsonValue ParseNumber()
            {
                int start = index;
                if (Consume('-') && index >= text.Length) throw Error("canonical_number_invalid", "A number is incomplete.");
                if (index < text.Length && text[index] == '0')
                {
                    index++;
                    if (index < text.Length && char.IsDigit(text[index])) throw Error("canonical_number_noncanonical", "Leading zeroes are not allowed.");
                }
                else
                {
                    RequireDigit();
                    while (index < text.Length && char.IsDigit(text[index])) index++;
                }
                bool fractional = false;
                if (Consume('.'))
                {
                    fractional = true;
                    if (index >= text.Length || !char.IsDigit(text[index])) throw Error("canonical_number_invalid", "A fraction requires digits.");
                    while (index < text.Length && char.IsDigit(text[index])) index++;
                }
                if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
                {
                    fractional = true;
                    index++;
                    if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                    if (index >= text.Length || !char.IsDigit(text[index])) throw Error("canonical_number_invalid", "An exponent requires digits.");
                    while (index < text.Length && char.IsDigit(text[index])) index++;
                }
                string raw = text.Substring(start, index - start);
                if (!fractional)
                {
                    long integer;
                    if (!long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out integer) || integer.ToString(CultureInfo.InvariantCulture) != raw) throw Error("canonical_number_noncanonical", "Integer numbers must use invariant decimal form without leading zeroes.");
                    return new JsonValue { Kind = JsonKind.Number, Scalar = raw };
                }
                double number;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || double.IsNaN(number) || double.IsInfinity(number)) throw Error("canonical_number_nonfinite", "Numbers must be finite.");
                string canonical = number.ToString("R", CultureInfo.InvariantCulture);
                if (!StringComparer.Ordinal.Equals(raw, canonical)) throw Error("canonical_number_noncanonical", "Finite decimal numbers must use invariant round-trip form.");
                return new JsonValue { Kind = JsonKind.Number, Scalar = canonical };
            }

            private string ParseString()
            {
                Require('"');
                var builder = new StringBuilder();
                while (index < text.Length)
                {
                    char character = text[index++];
                    if (character == '"') return Normalize(builder.ToString());
                    if (character < 0x20) throw Error("canonical_json_invalid", "Control characters must be escaped.");
                    if (character != '\\') { builder.Append(character); continue; }
                    if (index >= text.Length) throw Error("canonical_json_invalid", "An escape sequence is incomplete.");
                    char escaped = text[index++];
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
                        case 'u': builder.Append(ParseUnicode()); break;
                        default: throw Error("canonical_json_invalid", "Unknown escape sequence.");
                    }
                }
                throw Error("canonical_json_unexpected_end", "A JSON string is incomplete.");
            }

            private char ParseUnicode()
            {
                if (index + 4 > text.Length) throw Error("canonical_json_invalid", "A unicode escape is incomplete.");
                int value = 0;
                for (var count = 0; count < 4; count++)
                {
                    int digit = HexDigit(text[index++]);
                    if (digit < 0) throw Error("canonical_json_invalid", "A unicode escape contains an invalid digit.");
                    value = (value * 16) + digit;
                }
                return (char)value;
            }

            private void SkipWhitespace() { while (index < text.Length && (text[index] == ' ' || text[index] == '\t' || text[index] == '\r' || text[index] == '\n')) index++; }
            private bool Consume(char character) { if (index < text.Length && text[index] == character) { index++; return true; } return false; }
            private void Require(char character) { if (!Consume(character)) throw Error("canonical_json_invalid", "Expected '" + character + "'."); }
            private void RequireDigit() { if (index >= text.Length || !char.IsDigit(text[index]) || text[index] == '0') throw Error("canonical_number_invalid", "A number requires a non-zero leading digit."); }
            private CanonicalizationException Error(string code, string message) { return new CanonicalizationException(code, message); }
            private static int HexDigit(char character)
            {
                if (character >= '0' && character <= '9') return character - '0';
                if (character >= 'a' && character <= 'f') return character - 'a' + 10;
                if (character >= 'A' && character <= 'F') return character - 'A' + 10;
                return -1;
            }
        }

        private sealed class CanonicalizationException : Exception
        {
            internal CanonicalizationException(string code, string message) : base(message) { Code = code; }
            internal string Code { get; }
        }
    }
}
