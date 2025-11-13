using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Handles serialization of the aggregated game state returned by <see cref="GameState.SaveGame"/>.
    /// Saves and loads JSON files inside Unity's persistent data path by default.
    /// </summary>
    public class SaveService
    {
        public const string DefaultFileName = "autosave.json";

        private readonly string saveDirectory;
        private readonly string defaultFileName;

        public SaveService(string saveDirectory = null, string defaultFileName = DefaultFileName)
        {
            this.saveDirectory = string.IsNullOrWhiteSpace(saveDirectory)
                ? Application.persistentDataPath
                : saveDirectory;
            this.defaultFileName = string.IsNullOrWhiteSpace(defaultFileName)
                ? DefaultFileName
                : defaultFileName.Trim();
        }

        public string Save(GameState state, string fileName = null)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var payload = state.SaveGame();
            if (payload is not Dictionary<string, object> data)
            {
                Logger.Warn("SaveService", "GameState.SaveGame() did not return a Dictionary<string, object>.");
                return null;
            }

            string target = ResolvePath(fileName);

            try
            {
                if (string.IsNullOrWhiteSpace(saveDirectory))
                {
                    Logger.Warn("SaveService", "Persistent data path unavailable; cannot write save file.");
                    return null;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? saveDirectory);

                var blob = new Dictionary<string, object>
                {
                    ["version"] = 1,
                    ["timestamp"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    ["state"] = data
                };

                string json = MiniJson.Serialize(blob);
                File.WriteAllText(target, json, Encoding.UTF8);
                Logger.Info("SaveService", $"Game saved to {target}.");
                return target;
            }
            catch (Exception ex)
            {
                Logger.Error("SaveService", $"Failed to write save file: {ex.Message}");
                return null;
            }
        }

        public bool LoadInto(GameState state, string fileName = null)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            string target = ResolvePath(fileName);
            if (!File.Exists(target))
            {
                Logger.Warn("SaveService", $"Save file not found: {target}.");
                return false;
            }

            try
            {
                string json = File.ReadAllText(target, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Logger.Warn("SaveService", "Save file was empty.");
                    return false;
                }

                if (MiniJson.Deserialize(json) is not Dictionary<string, object> blob)
                {
                    Logger.Warn("SaveService", "Save file root was not a dictionary.");
                    return false;
                }

                if (!blob.TryGetValue("state", out var stateObj) || stateObj is not Dictionary<string, object> stateData)
                {
                    Logger.Warn("SaveService", "Save file did not contain expected 'state' section.");
                    return false;
                }

                state.LoadGame(stateData);
                Logger.Info("SaveService", $"Game loaded from {target}.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("SaveService", $"Failed to load save file: {ex.Message}");
                return false;
            }
        }

        public bool HasSave(string fileName = null)
        {
            string target = ResolvePath(fileName);
            return File.Exists(target);
        }

        public bool Delete(string fileName = null)
        {
            string target = ResolvePath(fileName);
            if (!File.Exists(target))
                return false;
            try
            {
                File.Delete(target);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("SaveService", $"Failed to delete save file: {ex.Message}");
                return false;
            }
        }

        private string ResolvePath(string fileName)
        {
            string name = string.IsNullOrWhiteSpace(fileName) ? defaultFileName : fileName.Trim();
            if (Path.IsPathRooted(name))
                return name;
            if (string.IsNullOrWhiteSpace(saveDirectory))
                return name;
            return Path.Combine(saveDirectory, name);
        }

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
                    var dict = new Dictionary<string, object>();

                    reader.Read(); // consume {
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
                        reader.Read(); // consume :
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
                    reader.Read(); // [
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
                    reader.Read(); // "
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
                private readonly StringBuilder builder = new();

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
}
