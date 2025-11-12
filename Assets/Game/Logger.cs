using UnityEngine;

namespace Game.Core
{
    public static class Logger
    {
        // Basic log — neutral white
        public static void Log(string system, string message)
        {
            Debug.Log(Format(system, message, "white"));
        }

        // Success / progression
        public static void Info(string system, string message)
        {
            Debug.Log(Format(system, message, "cyan"));
        }

        // Warnings
        public static void Warn(string system, string message)
        {
            Debug.LogWarning(Format(system, message, "yellow"));
        }

        // Errors
        public static void Error(string system, string message)
        {
            Debug.LogError(Format(system, message, "red"));
        }

        // Helper to format with system tag and color
        private static string Format(string system, string message, string color)
        {
            return $"<color={color}>[{system}]</color> {message}";
        }
    }
}
