using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Game.Core.Save
{
    public class SaveSerializer
    {
        public const int CurrentVersion = 1;

        private readonly IReadOnlyList<string> requiredSystemKeys;

        public SaveSerializer(IEnumerable<string> requiredSystemKeys = null)
        {
            requiredSystemKeys = (requiredSystemKeys ?? GetDefaultRequiredSystemKeys())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            this.requiredSystemKeys = Array.AsReadOnly(requiredSystemKeys.ToArray());
        }

        public IReadOnlyList<string> RequiredSystemKeys => requiredSystemKeys;

        public SaveData CreateSaveData(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var payload = state.SaveGame();
            if (payload is not Dictionary<string, object> systems)
                throw new InvalidOperationException("GameState.SaveGame() did not return a Dictionary<string, object>.");

            var data = new SaveData
            {
                Version = CurrentVersion,
                TimestampUtc = DateTime.UtcNow,
                Systems = new Dictionary<string, object>(systems, StringComparer.Ordinal)
            };

            return data;
        }

        public string Serialize(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var validation = Validate(data);
            if (!validation.IsValid)
                throw new SaveDataValidationException("SaveData failed validation.", validation);

            var root = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["version"] = data.Version,
                ["timestamp"] = data.TimestampUtc.ToString("o", CultureInfo.InvariantCulture),
                ["state"] = data.Systems ?? new Dictionary<string, object>(StringComparer.Ordinal)
            };

            if (data.Metadata != null && data.Metadata.Count > 0)
                root["metadata"] = data.Metadata;

            return MiniJson.Serialize(root);
        }

        public SaveData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON payload was null or empty.", nameof(json));

            if (MiniJson.Deserialize(json) is not Dictionary<string, object> root)
                throw new InvalidOperationException("Save payload was not a JSON object.");

            int version = ExtractInt(root, "version");
            DateTime timestamp = ExtractDateTime(root, "timestamp");
            var systems = ExtractDictionary(root, "state") ?? new Dictionary<string, object>(StringComparer.Ordinal);
            var metadata = ExtractDictionary(root, "metadata") ?? new Dictionary<string, object>(StringComparer.Ordinal);

            return new SaveData
            {
                Version = version,
                TimestampUtc = timestamp,
                Systems = systems,
                Metadata = metadata
            };
        }

        public void ApplyToGameState(GameState state, SaveData data)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (data == null) throw new ArgumentNullException(nameof(data));

            var validation = Validate(data);
            if (!validation.IsValid)
                throw new SaveDataValidationException("SaveData failed validation.", validation);

            state.LoadGame(data.Systems);
        }

        public SaveValidationResult Validate(SaveData data)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (data == null)
            {
                errors.Add("SaveData was null.");
                return new SaveValidationResult(errors, warnings);
            }

            if (data.Version <= 0)
            {
                errors.Add("SaveData version must be greater than zero.");
            }
            else if (data.Version != CurrentVersion)
            {
                warnings.Add($"SaveData version {data.Version} differs from expected version {CurrentVersion}.");
            }

            if (data.TimestampUtc == default)
                errors.Add("SaveData timestamp was not set.");

            if (data.Systems == null || data.Systems.Count == 0)
            {
                errors.Add("SaveData did not contain any system state.");
            }
            else
            {
                foreach (var key in requiredSystemKeys)
                {
                    if (!data.Systems.ContainsKey(key))
                        errors.Add($"Required system state '{key}' was missing from SaveData.");
                }
            }

            return new SaveValidationResult(errors, warnings);
        }

        private static int ExtractInt(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out var value))
                throw new InvalidDataException($"Save payload missing required field '{key}'.");

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Field '{key}' was not a valid integer.", ex);
            }
        }

        private static DateTime ExtractDateTime(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out var value))
                throw new InvalidDataException($"Save payload missing required field '{key}'.");

            if (value is string s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                return dt;

            throw new InvalidDataException($"Field '{key}' was not a valid timestamp.");
        }

        private static Dictionary<string, object> ExtractDictionary(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out var value) || value == null)
                return null;

            if (value is Dictionary<string, object> dict)
                return new Dictionary<string, object>(dict, StringComparer.Ordinal);

            if (value is IDictionary<string, object> generic)
                return new Dictionary<string, object>(generic, StringComparer.Ordinal);

            if (value is IDictionary dictionary)
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string name)
                        result[name] = entry.Value;
                }
                return result;
            }

            throw new InvalidDataException($"Field '{key}' was not a JSON object.");
        }

        private static IReadOnlyList<string> GetDefaultRequiredSystemKeys() => new[]
        {
            typeof(Game.Systems.Time.TimeSystem).FullName,
            typeof(Game.Systems.CharacterSystem.CharacterSystem).FullName,
            typeof(Game.Systems.Politics.Offices.OfficeSystem).FullName,
            typeof(Game.Systems.Politics.Elections.ElectionSystem).FullName,
            typeof(Game.Systems.BirthSystem.BirthSystem).FullName,
            typeof(Game.Systems.MarriageSystem.MarriageSystem).FullName
        };

        private static class MiniJson
        {
            public static object Deserialize(string json)
            {
                if (string.IsNullOrWhiteSpace(json))
                    return null;
                return Parser.Parse(json);
            }

            public static string Serialize(object obj)
            {
                return Serializer.Serialize(obj);
            }

            private sealed class Parser : IDisposable
            {
                private readonly StringReader reader;

                private Parser(string json)
                {
                    reader = new StringReader(json);
                }

                public static object Parse(string json)
                {
                    using var parser = new Parser(json);
                    return parser.ParseValue();
                }

                public void Dispose()
                {
                    reader.Dispose();
                }

                private object ParseValue()
                {
                    EatWhitespace();
                    if (reader.Peek() == -1)
                        return null;

                    char c = (char)reader.Peek();
                    return c switch
                    {
                        '{' => ParseObject(),
                        '[' => ParseArray(),
                        '"' => ParseString(),
                        '-' or >= '0' and <= '9' => ParseNumber(),
                        _ => ParseLiteral(),
                    };
                }

                private Dictionary<string, object> ParseObject()
                {
                    var dict = new Dictionary<string, object>(StringComparer.Ordinal);

                    reader.Read();
                    while (true)
                    {
                        EatWhitespace();
                        if (reader.Peek() == -1)
                            break;
                        if ((char)reader.Peek() == '}')
                        {
                            reader.Read();
                            break;
                        }

                        string key = ParseString();
                        EatWhitespace();
                        reader.Read();
                        object value = ParseValue();
                        dict[key] = value;
                        EatWhitespace();
                        int next = reader.Peek();
                        if (next == ',')
                        {
                            reader.Read();
                            continue;
                        }
                        if (next == '}')
                        {
                            reader.Read();
                            break;
                        }
                    }

                    return dict;
                }

                private List<object> ParseArray()
                {
                    var list = new List<object>();
                    reader.Read();
                    bool done = false;
                    while (!done)
                    {
                        EatWhitespace();
                        if (reader.Peek() == -1)
                            break;
                        char c = (char)reader.Peek();
                        if (c == ']')
                        {
                            reader.Read();
                            break;
                        }
                        list.Add(ParseValue());
                        EatWhitespace();
                        int next = reader.Peek();
                        if (next == ',')
                        {
                            reader.Read();
                        }
                        else if (next == ']')
                        {
                            reader.Read();
                            done = true;
                        }
                    }
                    return list;
                }

                private string ParseString()
                {
                    var sb = new StringBuilder();
                    reader.Read();
                    while (true)
                    {
                        if (reader.Peek() == -1)
                            break;
                        char c = (char)reader.Read();
                        if (c == '"')
                            break;
                        if (c == '\\')
                        {
                            if (reader.Peek() == -1)
                                break;
                            c = (char)reader.Read();
                            c = c switch
                            {
                                '"' => '"',
                                '\\' => '\\',
                                '/' => '/',
                                'b' => '\b',
                                'f' => '\f',
                                'n' => '\n',
                                'r' => '\r',
                                't' => '\t',
                                'u' => ParseUnicode(),
                                _ => c
                            };
                        }
                        sb.Append(c);
                    }
                    return sb.ToString();
                }

                private char ParseUnicode()
                {
                    Span<char> buffer = stackalloc char[4];
                    for (int i = 0; i < 4; i++)
                    {
                        int next = reader.Read();
                        if (next == -1)
                            return '\0';
                        buffer[i] = (char)next;
                    }
                    if (int.TryParse(buffer, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                        return (char)code;
                    return '\0';
                }

                private object ParseNumber()
                {
                    var sb = new StringBuilder();
                    bool hasDecimal = false;
                    while (reader.Peek() != -1)
                    {
                        char c = (char)reader.Peek();
                        if ((c >= '0' && c <= '9') || c == '-' || c == '+')
                        {
                            sb.Append(c);
                            reader.Read();
                        }
                        else if (c == '.' || c == 'e' || c == 'E')
                        {
                            hasDecimal = true;
                            sb.Append(c);
                            reader.Read();
                        }
                        else
                        {
                            break;
                        }
                    }

                    string number = sb.ToString();
                    if (!hasDecimal && long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                        return l;
                    if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                        return d;
                    return 0d;
                }

                private object ParseLiteral()
                {
                    var sb = new StringBuilder();
                    while (reader.Peek() != -1)
                    {
                        char c = (char)reader.Peek();
                        if (char.IsLetter(c))
                        {
                            sb.Append(c);
                            reader.Read();
                        }
                        else
                        {
                            break;
                        }
                    }

                    string literal = sb.ToString();
                    return literal switch
                    {
                        "true" => true,
                        "false" => false,
                        "null" => null,
                        _ => literal
                    };
                }

                private void EatWhitespace()
                {
                    while (reader.Peek() != -1)
                    {
                        if (!char.IsWhiteSpace((char)reader.Peek()))
                            break;
                        reader.Read();
                    }
                }
            }

            private sealed class Serializer
            {
                private readonly StringBuilder builder = new StringBuilder();

                public static string Serialize(object obj)
                {
                    var serializer = new Serializer();
                    serializer.SerializeValue(obj);
                    return serializer.builder.ToString();
                }

                private void SerializeValue(object value)
                {
                    switch (value)
                    {
                        case null:
                            builder.Append("null");
                            break;
                        case string s:
                            SerializeString(s);
                            break;
                        case bool b:
                            builder.Append(b ? "true" : "false");
                            break;
                        case IDictionary<string, object> dict:
                            SerializeObject(dict);
                            break;
                        case IDictionary dictionary:
                            SerializeDictionary(dictionary);
                            break;
                        case IEnumerable enumerable when value is not string:
                            SerializeArray(enumerable);
                            break;
                        case char ch:
                            SerializeString(ch.ToString());
                            break;
                        case sbyte or byte or short or ushort or int or uint or long or ulong:
                            builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                            break;
                        case float or double or decimal:
                            builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                            break;
                        default:
                            SerializeString(value.ToString() ?? string.Empty);
                            break;
                    }
                }

                private void SerializeObject(IDictionary<string, object> dict)
                {
                    builder.Append('{');
                    bool first = true;
                    foreach (var kvp in dict)
                    {
                        if (!first)
                            builder.Append(',');
                        SerializeString(kvp.Key);
                        builder.Append(':');
                        SerializeValue(kvp.Value);
                        first = false;
                    }
                    builder.Append('}');
                }

                private void SerializeDictionary(IDictionary dict)
                {
                    builder.Append('{');
                    bool first = true;
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (entry.Key is not string key)
                            continue;
                        if (!first)
                            builder.Append(',');
                        SerializeString(key);
                        builder.Append(':');
                        SerializeValue(entry.Value);
                        first = false;
                    }
                    builder.Append('}');
                }

                private void SerializeArray(IEnumerable array)
                {
                    builder.Append('[');
                    bool first = true;
                    foreach (var element in array)
                    {
                        if (!first)
                            builder.Append(',');
                        SerializeValue(element);
                        first = false;
                    }
                    builder.Append(']');
                }

                private void SerializeString(string str)
                {
                    builder.Append('"');
                    foreach (var c in str)
                    {
                        switch (c)
                        {
                            case '"': builder.Append("\\\""); break;
                            case '\\': builder.Append("\\\\"); break;
                            case '\b': builder.Append("\\b"); break;
                            case '\f': builder.Append("\\f"); break;
                            case '\n': builder.Append("\\n"); break;
                            case '\r': builder.Append("\\r"); break;
                            case '\t': builder.Append("\\t"); break;
                            default:
                                if (c < ' ')
                                {
                                    builder.Append("\\u");
                                    builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                                }
                                else
                                {
                                    builder.Append(c);
                                }
                                break;
                        }
                    }
                    builder.Append('"');
                }
            }
        }
    }

    public sealed class SaveValidationResult
    {
        private readonly List<string> errors;
        private readonly List<string> warnings;

        public SaveValidationResult(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            this.errors = (errors ?? Enumerable.Empty<string>())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message.Trim())
                .ToList();
            this.warnings = (warnings ?? Enumerable.Empty<string>())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message.Trim())
                .ToList();
        }

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public bool IsValid => errors.Count == 0;
    }

    public sealed class SaveDataValidationException : Exception
    {
        public SaveValidationResult Result { get; }

        public SaveDataValidationException(string message, SaveValidationResult result)
            : base(message)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public SaveDataValidationException(string message, SaveValidationResult result, Exception inner)
            : base(message, inner)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }
    }
}
