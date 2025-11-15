using System;
using System.Globalization;
using System.Linq;

namespace Game.Data.Characters
{
    internal static class RomanNameUtility
    {
        private static readonly TextInfo TextInfo = CultureInfo.InvariantCulture.TextInfo;

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var segments = value
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeToken)
                .Where(token => !string.IsNullOrEmpty(token));

            var result = string.Join(" ", segments);
            return result.Length == 0 ? null : result;
        }

        public static string ToFeminine(string gens)
        {
            var clean = Normalize(gens);
            if (string.IsNullOrEmpty(clean))
                return null;

            if (clean.EndsWith("ius", StringComparison.OrdinalIgnoreCase))
                return clean[..^3] + "ia";
            if (clean.EndsWith("us", StringComparison.OrdinalIgnoreCase))
                return clean[..^2] + "a";
            if (clean.EndsWith("as", StringComparison.OrdinalIgnoreCase))
                return clean[..^2] + "a";
            if (clean.EndsWith("is", StringComparison.OrdinalIgnoreCase))
                return clean[..^2] + "is";
            return clean + "a";
        }

        public static string ToMasculine(string gens)
        {
            var clean = Normalize(gens);
            if (string.IsNullOrEmpty(clean))
                return null;

            if (clean.EndsWith("ius", StringComparison.OrdinalIgnoreCase))
                return clean;
            if (clean.EndsWith("ia", StringComparison.OrdinalIgnoreCase))
                return clean[..^2] + "ius";
            if (clean.EndsWith("a", StringComparison.OrdinalIgnoreCase))
                return clean[..^1] + "us";
            if (clean.EndsWith("is", StringComparison.OrdinalIgnoreCase))
                return clean;
            return clean + "us";
        }

        private static string NormalizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            token = token.Trim();
            if (token.Length == 0)
                return null;

            var lower = token.ToLowerInvariant();
            if (lower.Length == 1)
                return TextInfo.ToUpper(lower);

            return char.ToUpperInvariant(lower[0]) + lower[1..];
        }
    }
}
