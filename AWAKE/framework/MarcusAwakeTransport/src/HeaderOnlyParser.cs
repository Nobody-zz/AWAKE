using System;
using System.Collections.Generic;
using System.Text;

namespace MarcusAwakeTransport
{
    public sealed class HeaderOnlyEnvelope
    {
        internal HeaderOnlyEnvelope(PipeEnvelope envelope, IReadOnlyCollection<string> fields)
        {
            Envelope = envelope;
            Fields = fields;
        }

        public PipeEnvelope Envelope { get; }
        public IReadOnlyCollection<string> Fields { get; }

        public bool HasField(string name)
        {
            if (name == null || Fields == null) return false;
            foreach (var field in Fields) if (StringComparer.Ordinal.Equals(field, name)) return true;
            return false;
        }
    }

    public static class HeaderOnlyParser
    {
        public static bool TryParse(string json, out HeaderOnlyEnvelope value, out string error)
        {
            value = null;
            error = string.Empty;
            try
            {
                var parser = new Parser(json);
                value = parser.Parse();
                return true;
            }
            catch (StrictJsonException exception)
            {
                error = exception.Message;
                return false;
            }
            catch (Exception)
            {
                error = "header_decode_failed";
                return false;
            }
        }

        private sealed class Parser
        {
            private static readonly HashSet<string> AllowedFields = new HashSet<string>(new[]
            {
                "ack_status", "campaign_guid", "causation_id", "checksum", "checksum_algorithm", "connection_epoch", "correlation_id", "deadline_unix_milliseconds", "direction_nonce", "error_code", "event_index", "fence_proof", "instance_epoch", "message_id", "message_type", "non_durable", "outcome_kind", "owner_id", "payload", "payload_length", "payload_schema", "payload_sha256", "protocol_id", "protocol_major", "protocol_minor", "request_id", "sequence", "session_generation", "session_id", "task_scope", "timeline_id"
            }, StringComparer.Ordinal);

            private readonly string json;
            private readonly HashSet<string> fields = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<KeyValuePair<string, JsonNode>> properties = new List<KeyValuePair<string, JsonNode>>();
            private int index;
            private int propertyCount;
            private int arrayItemCount;

            internal Parser(string json)
            {
                this.json = json ?? throw new StrictJsonException("json_missing");
                try
                {
                    if (StrictJson.Utf8.GetByteCount(json) > ProtocolConstants.MaxFrameBytes) throw new StrictJsonException("json_too_large");
                }
                catch (EncoderFallbackException)
                {
                    throw new StrictJsonException("json_utf8_invalid");
                }
            }

            internal HeaderOnlyEnvelope Parse()
            {
                SkipWhitespace();
                Expect('{');
                SkipWhitespace();
                if (!TryConsume('}'))
                {
                    while (true)
                    {
                        SkipWhitespace();
                        if (IsAtEnd || json[index] != '"') throw new StrictJsonException("json_property_required");
                        var name = ReadString();
                        if (!AllowedFields.Contains(name)) throw new StrictJsonException("unknown_" + name);
                        if (!fields.Add(name)) throw new StrictJsonException("duplicate_" + name);
                        propertyCount++;
                        if (propertyCount > ProtocolConstants.MaxJsonProperties) throw new StrictJsonException("json_property_limit");
                        SkipWhitespace();
                        Expect(':');
                        SkipWhitespace();
                        var valueStart = index;
                        SkipValue(0);
                        var rawValue = json.Substring(valueStart, index - valueStart);
                        properties.Add(new KeyValuePair<string, JsonNode>(name, StringComparer.Ordinal.Equals(name, "payload") ? StrictJson.String(rawValue) : StrictJson.Parse(rawValue)));
                        SkipWhitespace();
                        if (TryConsume('}')) break;
                        Expect(',');
                        SkipWhitespace();
                        if (!IsAtEnd && json[index] == '}') throw new StrictJsonException("json_trailing_comma");
                    }
                }

                SkipWhitespace();
                if (!IsAtEnd) throw new StrictJsonException("json_trailing_data");
                var root = StrictJson.Object(properties);
                return new HeaderOnlyEnvelope(Decode(root), new List<string>(fields).AsReadOnly());
            }

            private PipeEnvelope Decode(JsonNode root)
            {
                JsonNode payloadNode;
                var hasPayload = StrictJson.TryGet(root, "payload", out payloadNode);
                JsonNode taskScopeNode;
                var envelope = new PipeEnvelope
                {
                    AckStatus = OptionalString(root, "ack_status"),
                    CampaignGuid = OptionalString(root, "campaign_guid"),
                    CausationId = OptionalString(root, "causation_id"),
                    Checksum = OptionalString(root, "checksum"),
                    ChecksumAlgorithm = OptionalString(root, "checksum_algorithm"),
                    ConnectionEpoch = OptionalLong(root, "connection_epoch"),
                    CorrelationId = OptionalString(root, "correlation_id"),
                    DeadlineUnixMilliseconds = OptionalLong(root, "deadline_unix_milliseconds"),
                    DirectionNonce = OptionalString(root, "direction_nonce"),
                    ErrorCode = OptionalString(root, "error_code"),
                    EventIndex = OptionalLong(root, "event_index"),
                    FenceProof = OptionalString(root, "fence_proof"),
                    InstanceEpoch = OptionalLong(root, "instance_epoch"),
                    MessageId = OptionalString(root, "message_id"),
                    MessageType = OptionalString(root, "message_type"),
                    NonDurable = OptionalBoolean(root, "non_durable"),
                    OutcomeKind = OptionalString(root, "outcome_kind"),
                    OwnerId = OptionalString(root, "owner_id"),
                    PayloadJson = hasPayload ? StrictJson.StringValue(payloadNode) : string.Empty,
                    PayloadLength = OptionalInt(root, "payload_length"),
                    PayloadSchema = OptionalString(root, "payload_schema"),
                    PayloadSha256 = OptionalString(root, "payload_sha256"),
                    ProtocolId = OptionalString(root, "protocol_id"),
                    ProtocolMajor = OptionalInt(root, "protocol_major"),
                    ProtocolMinor = OptionalInt(root, "protocol_minor"),
                    Sequence = OptionalLong(root, "sequence"),
                    SessionGeneration = OptionalLong(root, "session_generation"),
                    SessionId = OptionalString(root, "session_id"),
                    TimelineId = OptionalString(root, "timeline_id"),
                    RequestId = OptionalString(root, "request_id")
                };
                if (StrictJson.TryGet(root, "task_scope", out taskScopeNode)) envelope.TaskScope = DecodeTaskScope(taskScopeNode);
                return envelope;
            }

            private static TaskScopeEnvelope DecodeTaskScope(JsonNode node)
            {
                if (node == null || node.Kind != JsonNodeKind.Object) throw new StrictJsonException("task_scope_object_required");
                return new TaskScopeEnvelope
                {
                    IdempotencyKey = OptionalString(node, "idempotency_key"),
                    MessageId = OptionalString(node, "message_id"),
                    OutputSchemaId = OptionalString(node, "output_schema_id"),
                    OutputSchemaMajor = OptionalInt(node, "output_schema_major"),
                    OutputSchemaMinor = OptionalInt(node, "output_schema_minor"),
                    OwnerId = OptionalString(node, "owner_id"),
                    ProfileId = OptionalString(node, "profile_id"),
                    ProviderId = OptionalString(node, "provider_id"),
                    RequestPayloadHash = OptionalString(node, "request_payload_hash"),
                    RouteId = OptionalString(node, "route_id"),
                    SettlementRequirement = OptionalString(node, "settlement_requirement"),
                    TaskId = OptionalString(node, "task_id")
                };
            }

            private void SkipValue(int depth)
            {
                if (depth > ProtocolConstants.MaxJsonDepth) throw new StrictJsonException("json_depth_exceeded");
                SkipWhitespace();
                if (IsAtEnd) throw new StrictJsonException("json_value_missing");
                switch (json[index])
                {
                    case '{': SkipObject(depth); return;
                    case '[': SkipArray(depth); return;
                    case '"': ReadString(); return;
                    case 't': ConsumeLiteral("true"); return;
                    case 'f': ConsumeLiteral("false"); return;
                    case 'n': ConsumeLiteral("null"); return;
                    default: SkipNumber(); return;
                }
            }

            private void SkipObject(int depth)
            {
                Expect('{');
                SkipWhitespace();
                var names = new HashSet<string>(StringComparer.Ordinal);
                if (TryConsume('}')) return;
                while (true)
                {
                    SkipWhitespace();
                    if (IsAtEnd || json[index] != '"') throw new StrictJsonException("json_property_required");
                    var name = ReadString();
                    if (!names.Add(name)) throw new StrictJsonException("json_duplicate_property");
                    propertyCount++;
                    if (propertyCount > ProtocolConstants.MaxJsonProperties) throw new StrictJsonException("json_property_limit");
                    SkipWhitespace();
                    Expect(':');
                    SkipValue(depth + 1);
                    SkipWhitespace();
                    if (TryConsume('}')) return;
                    Expect(',');
                    SkipWhitespace();
                    if (!IsAtEnd && json[index] == '}') throw new StrictJsonException("json_trailing_comma");
                }
            }

            private void SkipArray(int depth)
            {
                Expect('[');
                SkipWhitespace();
                if (TryConsume(']')) return;
                while (true)
                {
                    arrayItemCount++;
                    if (arrayItemCount > ProtocolConstants.MaxJsonArrayItems) throw new StrictJsonException("json_array_limit");
                    SkipValue(depth + 1);
                    SkipWhitespace();
                    if (TryConsume(']')) return;
                    Expect(',');
                    SkipWhitespace();
                    if (!IsAtEnd && json[index] == ']') throw new StrictJsonException("json_trailing_comma");
                }
            }

            private string ReadString()
            {
                Expect('"');
                var builder = new StringBuilder();
                while (!IsAtEnd)
                {
                    var character = json[index++];
                    if (character == '"') return builder.ToString();
                    if (character < 0x20) throw new StrictJsonException("json_control_character");
                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (IsAtEnd) throw new StrictJsonException("json_escape_missing");
                    var escaped = json[index++];
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
                        case 'u': builder.Append(ReadUnicode()); break;
                        default: throw new StrictJsonException("json_escape_invalid");
                    }
                }

                throw new StrictJsonException("json_string_unclosed");
            }

            private char ReadUnicode()
            {
                if (index + 4 > json.Length) throw new StrictJsonException("json_unicode_invalid");
                var value = 0;
                for (var offset = 0; offset < 4; offset++)
                {
                    var digit = json[index++];
                    value <<= 4;
                    if (digit >= '0' && digit <= '9') value += digit - '0';
                    else if (digit >= 'a' && digit <= 'f') value += digit - 'a' + 10;
                    else if (digit >= 'A' && digit <= 'F') value += digit - 'A' + 10;
                    else throw new StrictJsonException("json_unicode_invalid");
                }

                return (char)value;
            }

            private void SkipNumber()
            {
                var start = index;
                TryConsume('-');
                if (TryConsume('0'))
                {
                    if (!IsAtEnd && char.IsDigit(json[index])) throw new StrictJsonException("json_number_invalid");
                }
                else
                {
                    if (IsAtEnd || json[index] < '1' || json[index] > '9') throw new StrictJsonException("json_number_invalid");
                    while (!IsAtEnd && char.IsDigit(json[index])) index++;
                }
                if (TryConsume('.'))
                {
                    if (IsAtEnd || !char.IsDigit(json[index])) throw new StrictJsonException("json_number_invalid");
                    while (!IsAtEnd && char.IsDigit(json[index])) index++;
                }
                if (TryConsume('e') || TryConsume('E'))
                {
                    TryConsume('+');
                    TryConsume('-');
                    if (IsAtEnd || !char.IsDigit(json[index])) throw new StrictJsonException("json_number_invalid");
                    while (!IsAtEnd && char.IsDigit(json[index])) index++;
                }
                if (start == index) throw new StrictJsonException("json_number_invalid");
            }

            private void ConsumeLiteral(string literal)
            {
                if (index + literal.Length > json.Length || !string.Equals(json.Substring(index, literal.Length), literal, StringComparison.Ordinal)) throw new StrictJsonException("json_literal_invalid");
                index += literal.Length;
            }

            private static string OptionalString(JsonNode node, string name)
            {
                return StrictJson.TryGet(node, name, out _) ? StrictJson.OptionalString(node, name) : string.Empty;
            }

            private static int OptionalInt(JsonNode node, string name)
            {
                return StrictJson.TryGet(node, name, out _) ? StrictJson.RequiredInt(node, name) : 0;
            }

            private static long OptionalLong(JsonNode node, string name)
            {
                return StrictJson.TryGet(node, name, out _) ? StrictJson.RequiredLong(node, name) : 0;
            }

            private static bool OptionalBoolean(JsonNode node, string name)
            {
                return StrictJson.TryGet(node, name, out _) ? StrictJson.OptionalBoolean(node, name) : false;
            }

            private bool IsAtEnd => index >= json.Length;

            private void SkipWhitespace()
            {
                while (!IsAtEnd && char.IsWhiteSpace(json[index])) index++;
            }

            private void Expect(char expected)
            {
                if (IsAtEnd || json[index] != expected) throw new StrictJsonException("json_expected_" + expected);
                index++;
            }

            private bool TryConsume(char value)
            {
                if (IsAtEnd || json[index] != value) return false;
                index++;
                return true;
            }
        }
    }
}
