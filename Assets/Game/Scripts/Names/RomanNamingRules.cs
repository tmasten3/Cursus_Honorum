using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Data.Characters
{
    public static class RomanNamingRules
    {
        private static readonly System.Random rng = new System.Random();
        private static readonly RomanGensRegistry Registry = new(RomanGensData.All);

        private static readonly string[] DefaultPraenomina =
        {
            "Gaius", "Lucius", "Marcus", "Publius", "Quintus",
            "Tiberius", "Aulus", "Sextus", "Servius", "Spurius"
        };

        private static readonly string[] CognomenSuffixes =
        {
            "ianus", "inus", "illus", "ellus", "ullus", "icus", "inus", "enus", "ianus"
        };

        public static RomanName GenerateRomanName(Gender gender, string gens = null, SocialClass socialClass = SocialClass.Patrician)
        {
            return NormalizeOrGenerateName(gender, socialClass, gens, null);
        }

        public static RomanName GenerateStandaloneName(
            Gender gender,
            SocialClass socialClass,
            string gens = null,
            System.Random randomOverride = null)
        {
            var random = randomOverride ?? rng;
            var variant = ResolveVariantForGeneration(socialClass, gens, null, gender, random)
                          ?? Registry.GetRandomVariant(NormalizeSocialClass(socialClass), random)
                          ?? Registry.GetRandomVariant(SocialClass.Patrician, random)
                          ?? Registry.GetRandomVariant(SocialClass.Plebeian, random);

            return GenerateNameFromVariant(
                gender,
                variant,
                null,
                random,
                null,
                allowCreateCognomen: true);
        }

        public static RomanName GenerateChildName(
            Character father,
            Character mother,
            Gender gender,
            SocialClass socialClass,
            System.Random randomOverride = null)
        {
            var random = randomOverride ?? rng;
            var familySeed = father?.Family ?? mother?.Family;
            var templateNomen = father?.RomanName?.Nomen ?? mother?.RomanName?.Nomen;
            var variant = ResolveVariantForGeneration(socialClass, familySeed, templateNomen, gender, random);
            if (variant == null)
            {
                variant = Registry.GetRandomVariant(NormalizeSocialClass(socialClass), random)
                          ?? Registry.GetRandomVariant(SocialClass.Patrician, random)
                          ?? Registry.GetRandomVariant(SocialClass.Plebeian, random);
            }

            var inheritedCognomen = father?.RomanName?.Cognomen;
            if (string.IsNullOrWhiteSpace(inheritedCognomen))
                inheritedCognomen = mother?.RomanName?.Cognomen;

            return GenerateNameFromVariant(gender, variant, inheritedCognomen, random, null, allowCreateCognomen: true);
        }

        public static RomanName NormalizeOrGenerateName(
            Gender gender,
            SocialClass socialClass,
            string gens,
            RomanName template,
            List<string> corrections = null,
            System.Random randomOverride = null)
        {
            var random = randomOverride ?? rng;
            var normalizedFamily = Registry.NormalizeFamily(gens) ?? RomanNameUtility.Normalize(gens);
            var templatePraenomen = RomanNameUtility.Normalize(template?.Praenomen);
            var templateNomen = RomanNameUtility.Normalize(template?.Nomen);
            var templateCognomen = RomanNameUtility.Normalize(template?.Cognomen);

            var variant = ResolveVariantForGeneration(
                socialClass,
                normalizedFamily,
                templateNomen,
                gender,
                random);

            if (variant == null)
            {
                variant = Registry.GetRandomVariant(NormalizeSocialClass(socialClass), random)
                          ?? Registry.GetRandomVariant(SocialClass.Patrician, random)
                          ?? Registry.GetRandomVariant(SocialClass.Plebeian, random);
            }

            var request = new NamingRequest
            {
                TemplatePraenomen = templatePraenomen,
                TemplateCognomen = templateCognomen
            };

            return GenerateNameFromVariant(gender, variant, null, random, corrections, allowCreateCognomen: true, request);
        }

        public static string ResolveFamilyName(string family, RomanName name)
        {
            var candidate = Registry.NormalizeFamily(family);
            if (!string.IsNullOrEmpty(candidate))
                return candidate;

            if (name == null)
                return null;

            if (name.Gender == Gender.Male)
            {
                candidate = Registry.NormalizeFamily(name.Nomen) ?? RomanNameUtility.ToMasculine(name.Nomen);
            }
            else
            {
                var masculine = RomanNameUtility.ToMasculine(name.Nomen);
                candidate = Registry.NormalizeFamily(masculine) ?? masculine;
            }

            return RomanNameUtility.Normalize(candidate);
        }

        public static string GetFeminineForm(string gens) => RomanNameUtility.ToFeminine(gens);

        public static string GetMasculineForm(string gens) => RomanNameUtility.ToMasculine(gens);

        private static RomanGensVariant ResolveVariantForGeneration(
            SocialClass socialClass,
            string family,
            string templateNomen,
            Gender gender,
            System.Random random)
        {
            var variantClass = NormalizeSocialClass(socialClass);
            RomanGensDefinition definition = null;

            if (!string.IsNullOrEmpty(family))
                definition = Registry.GetDefinition(family);

            if (definition == null && !string.IsNullOrEmpty(templateNomen))
            {
                definition = Registry.GetDefinition(templateNomen);
                if (definition == null && gender == Gender.Female)
                {
                    var masculine = RomanNameUtility.ToMasculine(templateNomen);
                    definition = Registry.GetDefinition(masculine);
                }
            }

            var variant = definition?.GetVariantForClass(variantClass);
            if (variant != null)
                return variant;

            return Registry.GetRandomVariant(variantClass, random);
        }

        private static RomanName GenerateNameFromVariant(
            Gender gender,
            RomanGensVariant variant,
            string inheritedCognomen,
            System.Random random,
            List<string> corrections,
            bool allowCreateCognomen,
            NamingRequest request = null)
        {
            if (variant == null)
                throw new InvalidOperationException("Roman naming variant could not be resolved.");

            var definition = variant.Definition;
            string cognomen;

            if (request != null && !string.IsNullOrEmpty(request.TemplateCognomen))
            {
                cognomen = DetermineCognomenForNormalization(request.TemplateCognomen, variant, random, corrections, allowCreateCognomen);
            }
            else if (!string.IsNullOrEmpty(inheritedCognomen))
            {
                cognomen = DetermineChildCognomen(variant, inheritedCognomen, random);
            }
            else
            {
                cognomen = DetermineCognomenForNormalization(null, variant, random, corrections, allowCreateCognomen);
            }

            if (gender == Gender.Male)
            {
                string praenomen;
                if (request != null && variant.TryNormalizePraenomen(request.TemplatePraenomen, out var normalizedPraenomen))
                {
                    praenomen = normalizedPraenomen;
                }
                else
                {
                    if (!string.IsNullOrEmpty(request?.TemplatePraenomen))
                        corrections?.Add($"replaced praenomen '{request.TemplatePraenomen}' with gens-appropriate selection");
                    praenomen = variant.GetRandomPraenomen(random) ?? DefaultPraenomina[random.Next(DefaultPraenomina.Length)];
                }

                return new RomanName(praenomen, definition.StylizedNomen, cognomen, Gender.Male);
            }
            else
            {
                if (!string.IsNullOrEmpty(request?.TemplatePraenomen))
                    corrections?.Add("removed female praenomen");

                return new RomanName(null, definition.FeminineNomen, cognomen, Gender.Female);
            }
        }

        private static string DetermineCognomenForNormalization(
            string templateCognomen,
            RomanGensVariant variant,
            System.Random random,
            List<string> corrections,
            bool allowCreate)
        {
            if (!string.IsNullOrEmpty(templateCognomen))
            {
                variant.RegisterCognomen(templateCognomen);
                return RomanNameUtility.Normalize(templateCognomen);
            }

            var options = variant.GetAvailableCognomina().ToList();
            if (options.Count > 0)
                return options[random.Next(options.Count)];

            if (!allowCreate)
                return null;

            var created = GenerateNewCognomen(variant, random, null);
            variant.RegisterCognomen(created);
            corrections?.Add($"generated cognomen '{created}'");
            return created;
        }

        private static string DetermineChildCognomen(RomanGensVariant variant, string inherited, System.Random random)
        {
            var normalized = RomanNameUtility.Normalize(inherited);
            if (!string.IsNullOrEmpty(normalized))
            {
                variant.RegisterCognomen(normalized);
                if (ShouldInheritCognomen(random))
                    return normalized;
            }

            var alternatives = variant.GetAvailableCognomina()
                .Where(c => !string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (alternatives.Count > 0 && random.NextDouble() < 0.5)
                return alternatives[random.Next(alternatives.Count)];

            var created = GenerateNewCognomen(variant, random, null);
            variant.RegisterCognomen(created);
            return created;
        }

        private static string GenerateNewCognomen(RomanGensVariant variant, System.Random random, string seed)
        {
            var baseStem = variant?.Definition?.StylizedNomen ?? "Roman";
            baseStem = RomanNameUtility.Normalize(baseStem) ?? "Roman";

            if (baseStem.EndsWith("ius", StringComparison.OrdinalIgnoreCase))
                baseStem = baseStem[..^3];
            else if (baseStem.EndsWith("us", StringComparison.OrdinalIgnoreCase))
                baseStem = baseStem[..^2];

            if (!string.IsNullOrEmpty(seed))
            {
                var cleanSeed = RomanNameUtility.Normalize(seed);
                if (!string.IsNullOrEmpty(cleanSeed))
                    baseStem = cleanSeed;
            }

            if (baseStem.Length > 6)
            {
                int trim = random.Next(0, 3);
                if (trim > 0 && trim < baseStem.Length)
                    baseStem = baseStem[..^trim];
            }

            string candidate = null;
            int guard = 0;
            while (string.IsNullOrEmpty(candidate) || variant.ContainsCognomen(candidate))
            {
                var suffix = CognomenSuffixes[random.Next(CognomenSuffixes.Length)];
                candidate = RomanNameUtility.Normalize(baseStem + suffix);
                guard++;
                if (guard > 10)
                {
                    candidate = RomanNameUtility.Normalize(baseStem + "ianus");
                    break;
                }
            }

            return candidate ?? "Novus";
        }

        private static bool ShouldInheritCognomen(System.Random random)
        {
            int threshold = 90 + random.Next(0, 6);
            int roll = random.Next(0, 100);
            return roll < threshold;
        }

        private static SocialClass NormalizeSocialClass(SocialClass socialClass)
        {
            return socialClass == SocialClass.Equestrian ? SocialClass.Plebeian : socialClass;
        }

        private class NamingRequest
        {
            public Gender Gender;
            public string TemplatePraenomen;
            public string TemplateCognomen;
        }
    }
}
