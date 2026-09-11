using System;
using System.Collections.Generic;
using System.Text;

namespace MarcusAwakeTransport
{
    public static class StructuredJsonCanonicalizer
    {
        public static bool TryCanonicalizeObject(string json, out string canonicalJson, out string error)
        {
            canonicalJson = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "structured_json_missing";
                return false;
            }

            try
            {
                var encoding = new UTF8Encoding(false, true);
                if (encoding.GetByteCount(json) > 65_536)
                {
                    error = "structured_json_too_large";
                    return false;
                }

                var parser = new Parser(json);
                parser.ParseRootObject();
                parser.SkipWhitespace();
                if (!parser.IsAtEnd)
                {
                    error = "structured_json_trailing_data";
                    return false;
                }

                canonicalJson = parser.GetCanonicalJson();
                if (encoding.GetByteCount(canonicalJson) > 65_536)
                {
                    canonicalJson = string.Empty;
                    error = "structured_json_too_large";
                    return false;
                }

                return true;
            }
            catch (EncoderFallbackException)
            {
                error = "structured_json_invalid_utf8";
                return false;
            }
            catch (CanonicalizationException exception)
            {
                error = exception.Code;
                return false;
            }
        }

        private sealed class Parser
        {
            private readonly string json;
            private readonly StringBuilder output = new StringBuilder();
            private int index;
            private int propertyCount;

            internal Parser(string json)
            {
                this.json = json;
            }

            internal bool IsAtEnd => index >= json.Length;

            internal void ParseRootObject()
            {
                SkipWhitespace();
                if (IsAtEnd || json[index] != '{') throw Error("structured_json_object_required");
                ParseObject(0);
            }

            internal void SkipWhitespace()
            {
                while (!IsAtEnd)
                {
                    var value = json[index];
                    if (value != ' ' && value != '\t' && value != '\r' && value != '\n') return;
                    index++;
                }
            }

            internal string GetCanonicalJson()
            {
                return output.ToString();
            }

            private void ParseValue(int depth)
            {
                SkipWhitespace();
                if (depth > 16) throw Error("structured_json_depth_exceeded");
                if (IsAtEnd) throw Error("structured_json_value_missing");

                switch (json[index])
                {
                    case '{':
                        ParseObject(depth);
                        return;
                    case '[':
                        ParseArray(depth);
                        return;
                    case '"':
                        AppendString(ParseString());
                        return;
                    case 't':
                        ConsumeLiteral("true");
                        output.Append("true");
                        return;
                    case 'f':
                        ConsumeLiteral("false");
                        output.Append("false");
                        return;
                    case 'n':
                        ConsumeLiteral("null");
                        output.Append("null");
                        return;
                    default:
                        ParseNumber();
                        return;
                }
            }

            private void ParseObject(int depth)
            {
                Expect('{');
                output.Append('{');
                SkipWhitespace();
                var names = new HashSet<string>(StringComparer.Ordinal);
                if (TryConsume('}'))
                {
                    output.Append('}');
                    return;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (IsAtEnd || json[index] != '"') throw Error("structured_json_property_required");
                    var name = ParseString();
                    if (!names.Add(name)) throw Error("structured_json_duplicate_property");
                    propertyCount++;
                    if (propertyCount > 256) throw Error("structured_json_property_limit");
                    AppendString(name);
                    SkipWhitespace();
                    Expect(':');
                    output.Append(':');
                    ParseValue(depth + 1);
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        output.Append('}');
                        return;
                    }

                    Expect(',');
                    output.Append(',');
                    SkipWhitespace();
                    if (!IsAtEnd && json[index] == '}') throw Error("structured_json_trailing_comma");
                }
            }

            private void ParseArray(int depth)
            {
                Expect('[');
                output.Append('[');
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    output.Append(']');
                    return;
                }

                var itemCount = 0;
                while (true)
                {
                    itemCount++;
                    if (itemCount > 256) throw Error("structured_json_array_limit");
                    ParseValue(depth + 1);
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        output.Append(']');
                        return;
                    }

                    Expect(',');
                    output.Append(',');
                    SkipWhitespace();
                    if (!IsAtEnd && json[index] == ']') throw Error("structured_json_trailing_comma");
                }
            }

            private string ParseString()
            {
                Expect('"');
                var value = new StringBuilder();
                while (!IsAtEnd)
                {
                    var character = json[index++];
                    if (character == '"') return value.ToString();
                    if (character < 0x20) throw Error("structured_json_control_character");
                    if (character != '\\')
                    {
                        if (char.IsSurrogate(character))
                        {
                            if (!char.IsHighSurrogate(character) || IsAtEnd || !char.IsLowSurrogate(json[index])) throw Error("structured_json_invalid_utf8");
                            value.Append(character);
                            value.Append(json[index++]);
                        }
                        else
                        {
                            value.Append(character);
                        }
                        continue;
                    }

                    if (IsAtEnd) throw Error("structured_json_escape_invalid");
                    var escaped = json[index++];
                    switch (escaped)
                    {
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        case '/': value.Append('/'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'u':
                            var code = ReadHexCode();
                            if (char.IsHighSurrogate((char)code))
                            {
                                if (index + 5 >= json.Length || json[index] != '\\' || json[index + 1] != 'u') throw Error("structured_json_invalid_utf8");
                                index += 2;
                                var low = ReadHexCode();
                                if (!char.IsLowSurrogate((char)low)) throw Error("structured_json_invalid_utf8");
                                value.Append((char)code);
                                value.Append((char)low);
                            }
                            else if (char.IsLowSurrogate((char)code))
                            {
                                throw Error("structured_json_invalid_utf8");
                            }
                            else
                            {
                                value.Append((char)code);
                            }
                            break;
                        default:
                            throw Error("structured_json_escape_invalid");
                    }
                }

                throw Error("structured_json_string_unterminated");
            }

            private int ReadHexCode()
            {
                if (index + 4 > json.Length) throw Error("structured_json_escape_invalid");
                var value = 0;
                for (var offset = 0; offset < 4; offset++)
                {
                    var digit = HexValue(json[index++]);
                    if (digit < 0) throw Error("structured_json_escape_invalid");
                    value = (value << 4) | digit;
                }

                return value;
            }

            private void ParseNumber()
            {
                var start = index;
                if (TryConsume('-') && IsAtEnd) throw Error("structured_json_number_invalid");

                if (TryConsume('0'))
                {
                    if (!IsAtEnd && char.IsDigit(json[index])) throw Error("structured_json_number_invalid");
                }
                else
                {
                    if (IsAtEnd || json[index] < '1' || json[index] > '9') throw Error("structured_json_number_invalid");
                    while (!IsAtEnd && char.IsDigit(json[index])) index++;
                }

                if (TryConsume('.'))
                {
                    if (IsAtEnd || !char.IsDigit(json[index])) throw Error("structured_json_number_invalid");
                    while (!IsAtEnd && char.IsDigit(json[index])) index++;
                }

                if (!IsAtEnd && (json[index] == 'e' || json[index] == 'E'))
                {
                    index++;
                    if (!IsAtEnd && (json[index] == '+' || json[index] == '-')) index++;
                    if (IsAtEnd || !char.IsDigit(json[index])) throw Error("structured_json_number_invalid");
                    while (!IsAtEnd && char.IsDigit(json[index])) index++;
                }

                output.Append(json, start, index - start);
            }

            private void ConsumeLiteral(string literal)
            {
                if (index + literal.Length > json.Length || !StringComparer.Ordinal.Equals(json.Substring(index, literal.Length), literal)) throw Error("structured_json_literal_invalid");
                index += literal.Length;
            }

            private void AppendString(string value)
            {
                output.Append('"');
                for (var offset = 0; offset < value.Length; offset++)
                {
                    var character = value[offset];
                    switch (character)
                    {
                        case '"': output.Append("\\\""); break;
                        case '\\': output.Append("\\\\"); break;
                        case '\b': output.Append("\\b"); break;
                        case '\f': output.Append("\\f"); break;
                        case '\n': output.Append("\\n"); break;
                        case '\r': output.Append("\\r"); break;
                        case '\t': output.Append("\\t"); break;
                        default:
                            if (character < 0x20)
                            {
                                output.Append("\\u");
                                output.Append(((int)character).ToString("x4"));
                            }
                            else
                            {
                                output.Append(character);
                            }
                            break;
                    }
                }
                output.Append('"');
            }

            private void Expect(char expected)
            {
                if (IsAtEnd || json[index] != expected) throw Error("structured_json_syntax_invalid");
                index++;
            }

            private bool TryConsume(char value)
            {
                if (IsAtEnd || json[index] != value) return false;
                index++;
                return true;
            }

            private static int HexValue(char value)
            {
                if (value >= '0' && value <= '9') return value - '0';
                if (value >= 'a' && value <= 'f') return value - 'a' + 10;
                if (value >= 'A' && value <= 'F') return value - 'A' + 10;
                return -1;
            }

            private static CanonicalizationException Error(string code)
            {
                return new CanonicalizationException(code);
            }
        }

        private sealed class CanonicalizationException : Exception
        {
            internal CanonicalizationException(string code)
            {
                Code = code;
            }

            internal string Code { get; }
        }
    }
}
