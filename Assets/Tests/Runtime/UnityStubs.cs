using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Data.Characters;
using NUnit.Framework;

namespace CursusHonorum.Tests.Runtime
{
    internal static class UnityStubsNamespaceMarker
    {
    }
}

namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(object message) => Console.WriteLine(message);
        public static void LogWarning(object message) => Console.WriteLine(message);
        public static void LogError(object message) => Console.Error.WriteLine(message);
    }

    public static class Application
    {
        private static string _persistentDataPath = Path.GetTempPath();

        public static string persistentDataPath
        {
            get => _persistentDataPath;
            set => _persistentDataPath = string.IsNullOrWhiteSpace(value) ? Path.GetTempPath() : value;
        }
    }

    public static class Mathf
    {
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Clamp(float value, float min, float max)
        {
            if (min > max)
                return min;
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Pow(float f, float p) => MathF.Pow(f, p);

        public static float Sqrt(float f) => MathF.Sqrt(f);

        public static float Log10(float f) => MathF.Log10(f);
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
