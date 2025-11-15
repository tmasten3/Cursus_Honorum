using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Data.Characters.Generation
{
    internal static class BasePopulationGenerator
    {
        public const string GeneratorPrefix = "generated://";
        public const string DefaultKey = "generated://base_population";

        public static bool CanHandle(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.StartsWith(GeneratorPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static CharacterDataWrapper Generate(int seed)
        {
            var generator = new Generator(seed);
            return new CharacterDataWrapper
            {
                Characters = generator.BuildPopulation()
            };
        }

        private sealed class Generator
        {
            private readonly System.Random rng;
            private readonly RomanGensRegistry registry = new(RomanGensData.All);
            private readonly PopulationBuilder builder;
            private readonly HashSet<string> generatedBranches = new(StringComparer.OrdinalIgnoreCase);

            public Generator(int seed)
            {
                rng = new System.Random(seed);
                builder = new PopulationBuilder(rng, simulationYear: -248);
            }

            public List<Character> BuildPopulation()
            {
                foreach (var definition in registry.Definitions)
                {
                    foreach (var variant in definition.Variants)
                    {
                        GenerateVariantBranches(variant);
                    }
                }

                return builder.GetCharacters();
            }

            private void GenerateVariantBranches(RomanGensVariant variant)
            {
                var available = variant.GetAvailableCognomina().ToList();
                if (available.Count == 0)
                {
                    int fallbackBranches = 2 + rng.Next(0, 2); // 2-3 fallback branches
                    for (int i = 0; i < fallbackBranches; i++)
                        GenerateBranch(variant, null);
                    return;
                }

                foreach (var cognomen in available)
                    GenerateBranch(variant, cognomen);
            }

            private void GenerateBranch(RomanGensVariant variant, string preferredCognomen)
            {
                var founderIdentity = ResolveFounderIdentity(variant, preferredCognomen);
                var branchId = founderIdentity.BranchId ?? RomanNamingRules.ResolveBranchId(founderIdentity.Family, founderIdentity.Name);
                if (branchId != null && !generatedBranches.Add(branchId))
                    return;

                builder.CreateBranch(variant, founderIdentity);
            }

            private RomanIdentity ResolveFounderIdentity(RomanGensVariant variant, string preferredCognomen)
            {
                if (!string.IsNullOrEmpty(preferredCognomen))
                {
                    var template = new RomanName(null, variant.Definition.StylizedNomen, preferredCognomen, Gender.Male);
                    return RomanNamingRules.NormalizeOrGenerateIdentity(
                        Gender.Male,
                        variant.SocialClass,
                        variant.Definition.StylizedNomen,
                        template,
                        randomOverride: rng);
                }

                return RomanNamingRules.GenerateStandaloneIdentity(
                    Gender.Male,
                    variant.SocialClass,
                    variant.Definition.StylizedNomen,
                    rng);
            }
        }

        private sealed class PopulationBuilder
        {
            private readonly List<Character> characters = new();
            private readonly Dictionary<int, Character> byId = new();
            private readonly System.Random rng;
            private readonly int simulationYear;
            private int nextId = 1;

            private const int Gen3MarriageMinRatio = 30; // percentage
            private const int Gen3MarriageMaxRatio = 50; // percentage

            public PopulationBuilder(System.Random rng, int simulationYear)
            {
                this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
                this.simulationYear = simulationYear;
            }

            public List<Character> GetCharacters() => characters;

            public void CreateBranch(RomanGensVariant variant, RomanIdentity founderIdentity)
            {
                if (variant == null || !founderIdentity.IsValid)
                    return;

                var branchId = founderIdentity.BranchId ?? RomanNamingRules.ResolveBranchId(founderIdentity.Family, founderIdentity.Name);
                var founderAge = rng.Next(45, 63); // 45-62
                var founder = CreateMember(founderIdentity, Gender.Male, founderAge, variant.SocialClass, null, null, branchId, null);

                var lineageId = CreateLineageId(branchId, founder.ID);
                founder.LineageId = lineageId;

                var spouseIdentity = RomanNamingRules.NormalizeOrGenerateIdentity(
                    Gender.Female,
                    variant.SocialClass,
                    variant.Definition.StylizedNomen,
                    new RomanName(null, variant.Definition.FeminineNomen, founderIdentity.Cognomen, Gender.Female),
                    randomOverride: rng);

                var spouseAge = Math.Max(28, founderAge - rng.Next(2, 9));
                var spouse = CreateMember(spouseIdentity, Gender.Female, spouseAge, variant.SocialClass, null, null, branchId, lineageId);
                LinkSpouses(founder, spouse);

                var gen2Couples = BuildGenerationTwo(variant, founder, spouse, lineageId, branchId);
                var gen3 = BuildGenerationThree(variant, gen2Couples);
                BuildGenerationFour(variant, gen3);
            }

            private List<CoupleRecord> BuildGenerationTwo(
                RomanGensVariant variant,
                Character patriarch,
                Character matriarch,
                string lineageId,
                string branchId)
            {
                var couples = new List<CoupleRecord>();
                int childCount = rng.Next(2, 6); // 2-5 children

                var children = new List<Character>();
                for (int i = 0; i < childCount; i++)
                {
                    var gender = rng.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
                    var maxAge = Math.Max(18, Math.Min(55, patriarch.Age - 16 - rng.Next(0, 5)));
                    if (maxAge < 18)
                        maxAge = 18;
                    var age = rng.Next(18, maxAge + 1);

                    var identity = RomanNamingRules.GenerateChildIdentity(patriarch, matriarch, gender, variant.SocialClass, rng);
                    var child = CreateMember(identity, gender, age, variant.SocialClass, patriarch.ID, matriarch.ID, identity.BranchId ?? branchId, lineageId);
                    children.Add(child);
                }

                foreach (var child in children)
                {
                    if (!byId.TryGetValue(child.ID, out var childReference))
                        continue;

                    var spouseGender = childReference.Gender == Gender.Male ? Gender.Female : Gender.Male;
                    var cognomen = childReference.RomanName?.Cognomen;
                    var templateNomen = spouseGender == Gender.Male
                        ? variant.Definition.StylizedNomen
                        : variant.Definition.FeminineNomen;

                    var spouseIdentity = RomanNamingRules.NormalizeOrGenerateIdentity(
                        spouseGender,
                        variant.SocialClass,
                        variant.Definition.StylizedNomen,
                        new RomanName(null, templateNomen, cognomen, spouseGender),
                        randomOverride: rng);

                    var spouseAge = Math.Max(16, childReference.Age - rng.Next(0, 4) + rng.Next(0, 3));
                    var spouse = CreateMember(spouseIdentity, spouseGender, spouseAge, variant.SocialClass, null, null, spouseIdentity.BranchId ?? childReference.BranchId, null);

                    var malePartner = childReference.Gender == Gender.Male ? childReference : spouse;
                    var femalePartner = childReference.Gender == Gender.Male ? spouse : childReference;

                    var coupleBranch = malePartner.BranchId ?? childReference.BranchId;
                    var coupleLineage = CreateLineageId(coupleBranch, malePartner.ID);
                    malePartner.LineageId = coupleLineage;
                    femalePartner.LineageId = coupleLineage;

                    LinkSpouses(malePartner, femalePartner);
                    couples.Add(new CoupleRecord(malePartner, femalePartner, coupleBranch, coupleLineage));
                }

                return couples;
            }

            private List<Character> BuildGenerationThree(RomanGensVariant variant, List<CoupleRecord> gen2Couples)
            {
                var generation = new List<Character>();
                foreach (var couple in gen2Couples)
                {
                    int childCount = rng.Next(2, 5); // 2-4 children
                    for (int i = 0; i < childCount; i++)
                    {
                        var gender = rng.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
                        var maxAge = Math.Max(2, Math.Min(32, couple.Male.Age - 18 - rng.Next(0, 5)));
                        var minAge = 2;
                        if (maxAge < minAge)
                            maxAge = minAge;
                        var age = rng.Next(minAge, maxAge + 1);

                        var identity = RomanNamingRules.GenerateChildIdentity(couple.Male, couple.Female, gender, variant.SocialClass, rng);
                        var branchId = identity.BranchId ?? couple.BranchId;
                        var child = CreateMember(identity, gender, age, variant.SocialClass, couple.Male.ID, couple.Female.ID, branchId, couple.LineageId);
                        generation.Add(child);
                    }
                }

                return generation;
            }

            private void BuildGenerationFour(RomanGensVariant variant, List<Character> gen3)
            {
                if (gen3 == null || gen3.Count == 0)
                    return;

                var adults = gen3.Where(c => c != null && c.Age >= 16 && !c.SpouseID.HasValue).ToList();
                if (adults.Count == 0)
                    return;

                Shuffle(adults);
                int targetPercentage = Gen3MarriageMinRatio + rng.Next(0, Gen3MarriageMaxRatio - Gen3MarriageMinRatio + 1);
                int targetCount = Math.Max(1, adults.Count * targetPercentage / 100);

                for (int i = 0; i < targetCount && i < adults.Count; i++)
                {
                    var candidate = adults[i];
                    if (candidate == null || candidate.SpouseID.HasValue)
                        continue;

                    var spouseGender = candidate.Gender == Gender.Male ? Gender.Female : Gender.Male;
                    var cognomen = candidate.RomanName?.Cognomen;
                    var templateNomen = spouseGender == Gender.Male
                        ? variant.Definition.StylizedNomen
                        : variant.Definition.FeminineNomen;

                    var spouseIdentity = RomanNamingRules.NormalizeOrGenerateIdentity(
                        spouseGender,
                        variant.SocialClass,
                        variant.Definition.StylizedNomen,
                        new RomanName(null, templateNomen, cognomen, spouseGender),
                        randomOverride: rng);

                    var spouseAge = Math.Max(16, candidate.Age - rng.Next(0, 4) + rng.Next(0, 3));
                    var branchId = spouseIdentity.BranchId ?? candidate.BranchId;
                    var spouse = CreateMember(spouseIdentity, spouseGender, spouseAge, variant.SocialClass, null, null, branchId, null);

                    var malePartner = candidate.Gender == Gender.Male ? candidate : spouse;
                    var femalePartner = candidate.Gender == Gender.Male ? spouse : candidate;
                    var coupleLineage = CreateLineageId(branchId, malePartner.ID);
                    malePartner.LineageId = coupleLineage;
                    femalePartner.LineageId = coupleLineage;

                    LinkSpouses(malePartner, femalePartner);

                    int childCount = rng.Next(2, 5); // 2-4 children
                    for (int childIndex = 0; childIndex < childCount; childIndex++)
                    {
                        var gender = rng.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
                        int age = rng.Next(0, 13); // infants through early teens
                        var identity = RomanNamingRules.GenerateChildIdentity(malePartner, femalePartner, gender, variant.SocialClass, rng);
                        var childBranch = identity.BranchId ?? branchId;
                        var child = CreateMember(identity, gender, age, variant.SocialClass, malePartner.ID, femalePartner.ID, childBranch, coupleLineage);
                        if (age >= 16)
                            adults.Add(child);
                    }
                }
            }

            private Character CreateMember(
                RomanIdentity identity,
                Gender gender,
                int age,
                SocialClass socialClass,
                int? fatherId,
                int? motherId,
                string branchId,
                string lineageId)
            {
                var (birthYear, birthMonth, birthDay) = CreateBirthDate(age);
                var romanName = identity.Name;
                if (romanName != null)
                    romanName.Gender = gender;

                var family = identity.Family ?? RomanNamingRules.ResolveFamilyName(null, romanName) ?? romanName?.Nomen;
                branchId ??= identity.BranchId ?? RomanNamingRules.ResolveBranchId(family, romanName);

                var id = nextId++;
                var character = new Character
                {
                    ID = id,
                    RomanName = romanName,
                    Gender = gender,
                    BirthYear = birthYear,
                    BirthMonth = birthMonth,
                    BirthDay = birthDay,
                    Age = age,
                    IsAlive = true,
                    FatherID = fatherId,
                    MotherID = motherId,
                    Family = family,
                    BranchId = branchId,
                    LineageId = lineageId ?? CreateLineageId(branchId, id),
                    Class = socialClass,
                    Wealth = RollWealth(socialClass),
                    Influence = RollInfluence(socialClass),
                    Ambition = AmbitionProfile.CreateDefault(),
                    TraitRecords = new List<TraitRecord>()
                };

                CharacterFactory.EnsureLifecycleState(character, simulationYear);

                characters.Add(character);
                byId[id] = character;
                return character;
            }

            private void LinkSpouses(Character a, Character b)
            {
                if (a == null || b == null)
                    return;

                a.SpouseID = b.ID;
                b.SpouseID = a.ID;
            }

            private (int Year, int Month, int Day) CreateBirthDate(int age)
            {
                int month = rng.Next(1, 13);
                int day = rng.Next(1, 29);
                int year = simulationYear - age;
                return (year, month, day);
            }

            private int RollWealth(SocialClass socialClass)
            {
                return socialClass == SocialClass.Patrician
                    ? rng.Next(4000, 12000)
                    : rng.Next(300, 5000);
            }

            private int RollInfluence(SocialClass socialClass)
            {
                return socialClass == SocialClass.Patrician
                    ? rng.Next(4, 12)
                    : rng.Next(1, 8);
            }

            private void Shuffle<T>(IList<T> list)
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
            }

            private string CreateLineageId(string branchId, int rootId)
            {
                var normalizedBranch = string.IsNullOrWhiteSpace(branchId) ? "Unknown" : branchId;
                return $"{normalizedBranch}#{rootId}";
            }
        }

        private readonly struct CoupleRecord
        {
            public CoupleRecord(Character male, Character female, string branchId, string lineageId)
            {
                Male = male;
                Female = female;
                BranchId = branchId;
                LineageId = lineageId;
            }

            public Character Male { get; }
            public Character Female { get; }
            public string BranchId { get; }
            public string LineageId { get; }
        }
    }
}
