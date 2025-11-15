using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Data.Characters
{
    internal sealed class RomanNamingService
    {
        private static readonly string[] DefaultPraenomina =
        {
            "Gaius", "Lucius", "Marcus", "Publius", "Quintus",
            "Tiberius", "Aulus", "Sextus", "Servius", "Spurius"
        };

        private static readonly string[] CognomenSuffixes =
        {
            "ianus", "inus", "illus", "ellus", "ullus", "icus", "enus"
        };

        public RomanNamingService(RomanNamingContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public RomanNamingContext Context { get; }

        public RomanNamingResult GenerateIdentity(
            Gender gender,
            SocialClass socialClass,
            string gens = null,
            string branchId = null,
            Random randomOverride = null)
        {
            var random = randomOverride ?? Context.Random;
            var branch = ResolveBranchForGeneration(gender, socialClass, gens, branchId, null, random, allowCreate: true, out _, out _);
            var name = BuildNameFromBranch(gender, branch, null, null, random, null, allowCreateCognomen: true, out bool _);
            return new RomanNamingResult(name, branch, false);
        }

        public RomanNamingResult GenerateChildIdentity(
            Character father,
            Character mother,
            Gender gender,
            SocialClass socialClass,
            Random randomOverride = null)
        {
            var random = randomOverride ?? Context.Random;
            var fatherBranch = Context.BranchRegistry.GetBranch(father?.BranchId);
            var motherBranch = Context.BranchRegistry.GetBranch(mother?.BranchId);

            var branch = DetermineChildBranch(fatherBranch, motherBranch, socialClass, father?.Family, mother?.Family, random, out bool created);
            var inheritedCognomen = father?.RomanName?.Cognomen ?? mother?.RomanName?.Cognomen;
            var name = BuildNameFromBranch(gender, branch, father?.RomanName?.Praenomen, inheritedCognomen, random, null, allowCreateCognomen: true, out bool branchCreatedDuringBuild);
            return new RomanNamingResult(name, branch, created || branchCreatedDuringBuild);
        }

        public RomanNamingResult NormalizeIdentity(
            Gender gender,
            SocialClass socialClass,
            string family,
            RomanName template,
            string branchId,
            List<string> corrections,
            Random randomOverride = null)
        {
            var random = randomOverride ?? Context.Random;
            var branch = ResolveBranchForGeneration(gender, socialClass, family, branchId, template?.Cognomen, random, allowCreate: true, out var masculineOverride, out var feminineOverride);

            var templatePraenomen = template?.Praenomen;
            var templateCognomen = template?.Cognomen;
            var resultName = BuildNameFromBranch(gender, branch, templatePraenomen, templateCognomen, random, corrections, allowCreateCognomen: true, out bool branchCreated);

            if (gender == Gender.Male && !string.IsNullOrEmpty(masculineOverride))
                resultName.Nomen = masculineOverride;
            else if (gender == Gender.Female && !string.IsNullOrEmpty(feminineOverride))
                resultName.Nomen = feminineOverride;

            return new RomanNamingResult(resultName, branch, branchCreated);
        }

        public string ResolveFamilyName(string fallbackFamily, RomanNamingResult result)
        {
            if (result?.Branch != null)
                return result.Branch.StylizedNomen;

            if (result?.Name != null)
                return RomanNameUtility.Normalize(result.Name.Nomen);

            return RomanNameUtility.Normalize(fallbackFamily);
        }

        public RomanFamilyBranch GetBranch(string branchId) => Context.BranchRegistry.GetBranch(branchId);

        public IReadOnlyList<RomanFamilyBranch> GetAllBranches() => Context.BranchRegistry.GetAllBranches();

        public RomanFamilyBranch CreateSupplementalBranch(Random randomOverride = null)
        {
            var random = randomOverride ?? Context.Random;
            var classOptions = new[] { SocialClass.Patrician, SocialClass.Plebeian };
            var socialClass = classOptions[random.Next(classOptions.Length)];
            var variant = Context.Registry.GetRandomVariant(socialClass, random)
                ?? Context.Registry.GetRandomVariant(SocialClass.Patrician, random)
                ?? Context.Registry.GetRandomVariant(SocialClass.Plebeian, random);
            if (variant == null)
                return null;

            return Context.BranchRegistry.CreateDynamicBranch(variant, null, v => GenerateNewCognomen(v, random, null));
        }

        private RomanFamilyBranch DetermineChildBranch(
            RomanFamilyBranch fatherBranch,
            RomanFamilyBranch motherBranch,
            SocialClass socialClass,
            string fatherFamily,
            string motherFamily,
            Random random,
            out bool created)
        {
            created = false;
            RomanFamilyBranch primary = fatherBranch ?? motherBranch;

            if (primary == null)
            {
                primary = ResolveBranchForGeneration(Gender.Male, socialClass, fatherFamily ?? motherFamily, null, null, random, allowCreate: true, out _, out _);
                return primary;
            }

            double inheritChance = 0.90 + random.NextDouble() * 0.05;
            if (random.NextDouble() <= inheritChance)
                return primary;

            var variant = primary.Variant;
            var alternatives = Context.BranchRegistry.GetBranchesForVariant(variant)
                .Where(b => !string.Equals(b.Id, primary.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (alternatives.Count > 0 && random.NextDouble() < 0.5)
                return alternatives[random.Next(alternatives.Count)];

            created = true;
            return Context.BranchRegistry.CreateDynamicBranch(variant, primary.Id, v => GenerateNewCognomen(v, random, primary.Cognomen));
        }

        private RomanFamilyBranch ResolveBranchForGeneration(
            Gender gender,
            SocialClass socialClass,
            string gens,
            string branchId,
            string cognomenTemplate,
            Random random,
            bool allowCreate,
            out string masculineOverride,
            out string feminineOverride)
        {
            masculineOverride = null;
            feminineOverride = null;

            if (!string.IsNullOrWhiteSpace(branchId))
            {
                var existing = Context.BranchRegistry.GetBranch(branchId);
                if (existing != null)
                    return existing;
            }

            var normalizedFamily = RomanNameUtility.Normalize(gens);
            RomanGensDefinition definition = null;
            if (!string.IsNullOrEmpty(normalizedFamily))
            {
                definition = Context.Registry.GetDefinition(normalizedFamily);
                if (definition == null)
                {
                    masculineOverride = RomanNameUtility.ToMasculine(normalizedFamily) ?? normalizedFamily;
                    feminineOverride = RomanNameUtility.ToFeminine(masculineOverride ?? normalizedFamily);
                }
            }

            var variant = definition?.GetVariantForClass(NormalizeSocialClass(socialClass));
            if (variant == null)
            {
                variant = Context.Registry.GetRandomVariant(NormalizeSocialClass(socialClass), random)
                          ?? Context.Registry.GetRandomVariant(SocialClass.Patrician, random)
                          ?? Context.Registry.GetRandomVariant(SocialClass.Plebeian, random);
            }

            if (variant == null)
                throw new InvalidOperationException("Unable to resolve Roman gens variant for name generation.");

            var cognomenBranch = !string.IsNullOrEmpty(cognomenTemplate)
                ? Context.BranchRegistry.GetBranch(variant, cognomenTemplate)
                : null;

            if (cognomenBranch != null)
                return cognomenBranch;

            if (!string.IsNullOrEmpty(cognomenTemplate) && allowCreate)
            {
                return Context.BranchRegistry.RegisterBranch(variant, cognomenTemplate, true, null);
            }

            var branches = Context.BranchRegistry.GetBranchesForVariant(variant);
            if (branches.Count == 0)
            {
                var created = Context.BranchRegistry.CreateDynamicBranch(variant, null, v => GenerateNewCognomen(v, random, cognomenTemplate));
                return created;
            }

            return branches[random.Next(branches.Count)];
        }

        private RomanName BuildNameFromBranch(
            Gender gender,
            RomanFamilyBranch branch,
            string templatePraenomen,
            string templateCognomen,
            Random random,
            List<string> corrections,
            bool allowCreateCognomen,
            out bool branchCreated)
        {
            if (branch == null)
                throw new InvalidOperationException("Branch must be resolved before generating a name.");

            branchCreated = false;
            var variant = branch.Variant;
            string cognomen = DetermineCognomen(branch, templateCognomen, random, corrections, allowCreateCognomen, out bool newBranchCreated);
            branchCreated |= newBranchCreated;

            if (gender == Gender.Male)
            {
                string praenomen;
                if (!string.IsNullOrEmpty(templatePraenomen) && variant.TryNormalizePraenomen(templatePraenomen, out var normalizedPraenomen))
                {
                    praenomen = normalizedPraenomen;
                }
                else
                {
                    if (!string.IsNullOrEmpty(templatePraenomen))
                        corrections?.Add($"replaced praenomen '{templatePraenomen}' with gens-appropriate selection");
                    praenomen = variant.GetRandomPraenomen(random) ?? DefaultPraenomina[random.Next(DefaultPraenomina.Length)];
                }

                var name = new RomanName(praenomen, branch.StylizedNomen, cognomen, Gender.Male);
                name.Gender = Gender.Male;
                return name;
            }
            else
            {
                if (!string.IsNullOrEmpty(templatePraenomen))
                    corrections?.Add("removed female praenomen");

                var name = new RomanName(null, branch.FeminineNomen, cognomen, Gender.Female);
                name.Gender = Gender.Female;
                return name;
            }
        }

        private string DetermineCognomen(
            RomanFamilyBranch branch,
            string templateCognomen,
            Random random,
            List<string> corrections,
            bool allowCreate,
            out bool branchCreated)
        {
            branchCreated = false;
            if (!string.IsNullOrEmpty(templateCognomen))
            {
                var normalized = RomanNameUtility.Normalize(templateCognomen);
                if (!string.IsNullOrEmpty(normalized))
                {
                    var existing = Context.BranchRegistry.GetBranch(branch.Variant, normalized);
                    if (existing == null && allowCreate)
                    {
                        Context.BranchRegistry.RegisterBranch(branch.Variant, normalized, true, branch.Id);
                        branchCreated = true;
                    }
                    return normalized;
                }
            }

            if (!string.IsNullOrEmpty(branch.Cognomen))
                return branch.Cognomen;

            if (!allowCreate)
                return branch.Cognomen;

            branchCreated = true;
            var generated = GenerateNewCognomen(branch.Variant, random, null);
            Context.BranchRegistry.RegisterBranch(branch.Variant, generated, true, branch.Id);
            corrections?.Add($"generated cognomen '{generated}'");
            return generated;
        }

        private string GenerateNewCognomen(RomanGensVariant variant, Random random, string seed)
        {
            var baseStem = RomanNameUtility.Normalize(variant?.Definition?.StylizedNomen) ?? "Roman";
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
            while (string.IsNullOrEmpty(candidate))
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

        private static SocialClass NormalizeSocialClass(SocialClass socialClass)
        {
            return socialClass == SocialClass.Equestrian ? SocialClass.Plebeian : socialClass;
        }
    }
}
