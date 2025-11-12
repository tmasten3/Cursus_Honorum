using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(object message) => Console.WriteLine(message);
        public static void LogWarning(object message) => Console.WriteLine(message);
        public static void LogError(object message) => Console.Error.WriteLine(message);
    }

    public static class Mathf
    {
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
    }

    public static class Time
    {
        public static float deltaTime { get; set; } = 1f / 60f;
    }

    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        public static T FromJson<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default!;
            return JsonSerializer.Deserialize<T>(json, Options)!;
        }

        public static string ToJson(object obj)
        {
            return JsonSerializer.Serialize(obj, Options);
        }
    }
}
