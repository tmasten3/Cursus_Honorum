using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Data.Characters
{
    public static class RomanNamingRules
    {
        private static readonly System.Random rng = new();

        private static readonly string[] MalePraenomina =
        {
            "Gaius", "Lucius", "Marcus", "Publius", "Quintus",
            "Tiberius", "Aulus", "Sextus", "Servius", "Spurius"
        };

        private static readonly string[] PatricianNomina =
        {
            "Cornelius", "Claudius", "Fabius", "Aemilius", "Julius", "Valerius"
        };

        private static readonly string[] PlebeianNomina =
        {
            "Aurelius", "Sempronius", "Sulpicius", "Licinius", "Servilius"
        };

        private static readonly string[] EquestrianNomina =
        {
            "Antonius", "Flavius", "Caecilius", "Domitius", "Pompeius"
        };

        private static readonly string[] Cognomina =
        {
            "Scipio", "Nero", "Pulcher", "Lentulus", "Paullus",
            "Caesar", "Cato", "Gracchus", "Crassus", "Metellus"
        };

        public static RomanName GenerateRomanName(Gender gender, string gens = null, SocialClass socialClass = SocialClass.Patrician)
        {
            var canonicalGens = NormalizeComponent(gens);
            if (!string.IsNullOrEmpty(canonicalGens))
                canonicalGens = GetMasculineForm(canonicalGens);

            return NormalizeOrGenerateName(gender, socialClass, canonicalGens, null);
        }

        public static RomanName NormalizeOrGenerateName(
            Gender gender,
            SocialClass socialClass,
            string gens,
            RomanName template,
            List<string> corrections = null)
        {
            string canonicalGens = NormalizeComponent(gens);
            if (!string.IsNullOrEmpty(canonicalGens))
                canonicalGens = GetMasculineForm(canonicalGens);

            string praenomen = NormalizeComponent(template?.Praenomen);
            string nomen = NormalizeComponent(template?.Nomen);
            string cognomen = NormalizeComponent(template?.Cognomen);

            if (gender == Gender.Male)
            {
                if (string.IsNullOrEmpty(nomen))
                {
                    nomen = canonicalGens ?? GetNomen(socialClass);
                    corrections?.Add($"assigned nomen '{nomen}'");
                }
                else
                {
                    var masculine = GetMasculineForm(nomen);
                    if (!string.Equals(nomen, masculine, StringComparison.Ordinal))
                    {
                        corrections?.Add($"normalized nomen to '{masculine}'");
                        nomen = masculine;
                    }
                }

                if (string.IsNullOrEmpty(praenomen))
                {
                    praenomen = GetPraenomenDistinct(nomen);
                    corrections?.Add($"generated praenomen '{praenomen}'");
                }

                if (string.IsNullOrEmpty(cognomen))
                {
                    cognomen = GetDistinctCognomen(praenomen, nomen);
                    corrections?.Add($"generated cognomen '{cognomen}'");
                }

                if (string.Equals(praenomen, nomen, StringComparison.OrdinalIgnoreCase))
                {
                    var adjusted = GetPraenomenDistinct(nomen);
                    if (!string.Equals(adjusted, praenomen, StringComparison.Ordinal))
                    {
                        corrections?.Add($"adjusted praenomen to '{adjusted}' to avoid duplication");
                        praenomen = adjusted;
                    }
                }

                if (string.Equals(cognomen, nomen, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cognomen, praenomen, StringComparison.OrdinalIgnoreCase))
                {
                    var adjusted = GetDistinctCognomen(praenomen, nomen);
                    if (!string.Equals(adjusted, cognomen, StringComparison.Ordinal))
                    {
                        corrections?.Add($"adjusted cognomen to '{adjusted}' to avoid duplication");
                        cognomen = adjusted;
                    }
                }

                canonicalGens ??= nomen;
                return new RomanName(praenomen, nomen, cognomen, Gender.Male);
            }
            else
            {
                var baseGens = canonicalGens;

                if (string.IsNullOrEmpty(baseGens) && !string.IsNullOrEmpty(nomen))
                {
                    baseGens = GetMasculineForm(nomen);
                    corrections?.Add($"inferred gens '{baseGens}' from feminine nomen");
                }

                if (string.IsNullOrEmpty(baseGens))
                {
                    baseGens = GetNomen(socialClass);
                    corrections?.Add($"assigned gens '{baseGens}'");
                }

                var feminine = GetFeminineForm(baseGens);
                if (!string.IsNullOrEmpty(nomen) && !string.Equals(nomen, feminine, StringComparison.Ordinal))
                {
                    corrections?.Add($"normalized feminine nomen to '{feminine}'");
                }

                string finalCognomen = null;
                if (!string.IsNullOrEmpty(cognomen))
                {
                    if (IsNoble(socialClass))
                    {
                        if (string.Equals(cognomen, feminine, StringComparison.OrdinalIgnoreCase))
                        {
                            corrections?.Add("removed duplicate cognomen");
                        }
                        else
                        {
                            finalCognomen = cognomen;
                        }
                    }
                    else
                    {
                        corrections?.Add("removed cognomen for non-noble female");
                    }
                }

                return new RomanName(null, feminine, finalCognomen, Gender.Female);
            }
        }

        public static string GetFullName(Character c) => c?.RomanName?.GetFullName() ?? string.Empty;

        public static string GetFeminineForm(string gens)
        {
            if (string.IsNullOrWhiteSpace(gens))
                return null;

            var clean = NormalizeComponent(gens);
            if (string.IsNullOrEmpty(clean))
                return null;

            if (clean.EndsWith("ius", StringComparison.OrdinalIgnoreCase))
                return clean[..^3] + "ia";
            if (clean.EndsWith("us", StringComparison.OrdinalIgnoreCase))
                return clean[..^2] + "a";
            if (clean.EndsWith("as", StringComparison.OrdinalIgnoreCase))
                return clean[..^2] + "a";
            return clean + "a";
        }

        public static string GetMasculineForm(string gens)
        {
            if (string.IsNullOrWhiteSpace(gens))
                return null;

            var clean = NormalizeComponent(gens);
            if (string.IsNullOrEmpty(clean))
                return null;

            if (clean.EndsWith("ia", StringComparison.OrdinalIgnoreCase))
                return clean[..^2] + "ius";
            if (clean.EndsWith("a", StringComparison.OrdinalIgnoreCase))
                return clean[..^1] + "us";
            return clean;
        }

        public static string ResolveFamilyName(string family, RomanName name)
        {
            var candidate = GetMasculineForm(family);
            if (!string.IsNullOrEmpty(candidate))
                return candidate;

            if (name == null)
                return null;

            if (!string.IsNullOrEmpty(name.Nomen))
            {
                return name.Gender == Gender.Female
                    ? GetMasculineForm(name.Nomen)
                    : NormalizeComponent(name.Nomen);
            }

            return null;
        }

        public static string GetPraenomen() =>
            MalePraenomina[rng.Next(MalePraenomina.Length)];

        public static string GetNomen(SocialClass socialClass)
        {
            string[] pool = socialClass switch
            {
                SocialClass.Patrician => PatricianNomina,
                SocialClass.Plebeian => PlebeianNomina,
                SocialClass.Equestrian => EquestrianNomina,
                _ => PatricianNomina
            };
            return pool[rng.Next(pool.Length)];
        }

        public static string GetCognomen() =>
            Cognomina[rng.Next(Cognomina.Length)];

        private static string GetPraenomenDistinct(string nomen)
        {
            var guard = 0;
            var praenomen = GetPraenomen();
            while (string.Equals(praenomen, nomen, StringComparison.OrdinalIgnoreCase) && guard++ < 10)
                praenomen = GetPraenomen();
            return praenomen;
        }

        private static string GetDistinctCognomen(string praenomen, string nomen)
        {
            var guard = 0;
            var cognomen = GetCognomen();
            while ((string.Equals(cognomen, nomen, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cognomen, praenomen, StringComparison.OrdinalIgnoreCase))
                   && guard++ < 10)
            {
                cognomen = GetCognomen();
            }
            return cognomen;
        }

        private static string NormalizeComponent(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var segments = value
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ToTitleCase);

            return string.Join(" ", segments);
        }

        private static string ToTitleCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            value = value.Trim();
            if (value.Length == 0)
                return value;

            var lower = value.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower[1..];
        }

        private static bool IsNoble(SocialClass socialClass) =>
            socialClass == SocialClass.Patrician || socialClass == SocialClass.Equestrian;
    }
}
