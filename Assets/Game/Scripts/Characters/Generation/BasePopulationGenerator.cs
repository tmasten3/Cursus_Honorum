using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Data.Characters.Generation
{
    internal sealed class BasePopulationGenerator
    {
        private const string DescriptorKey = "base_characters";
        private readonly int seed;
        private readonly int startYear;

        private BasePopulationGenerator(int seed, int startYear)
        {
            this.seed = seed;
            this.startYear = startYear;
        }

        public static bool TryCreate(string descriptor, out BasePopulationGenerator generator)
        {
            generator = null;
            if (string.IsNullOrWhiteSpace(descriptor))
                return false;

            if (!descriptor.StartsWith("generator:", StringComparison.OrdinalIgnoreCase))
                return false;

            var payload = descriptor.Substring("generator:".Length);
            var parts = payload.Split(new[] { '?' }, 2);
            var key = parts[0]?.Trim();
            if (!string.Equals(key, DescriptorKey, StringComparison.OrdinalIgnoreCase))
                return false;

            int seed = 1337;
            int startYear = -248;

            if (parts.Length > 1)
            {
                var segments = parts[1].Split('&');
                foreach (var segment in segments)
                {
                    if (string.IsNullOrWhiteSpace(segment))
                        continue;

                    var trimmed = segment.Trim();
                    var pair = trimmed.Split(new[] { '=' }, 2);
                    if (pair.Length == 0)
                        continue;

                    var name = pair[0].Trim().ToLowerInvariant();
                    var value = pair.Length > 1 ? pair[1].Trim() : string.Empty;
                    if (name == "seed" && int.TryParse(value, out var parsedSeed))
                        seed = parsedSeed;
                    else if ((name == "startyear" || name == "start_year") && int.TryParse(value, out var parsedYear))
                        startYear = parsedYear;
                }
            }

            generator = new BasePopulationGenerator(seed, startYear);
            return true;
        }

        public CharacterDataWrapper Generate()
        {
            RomanNamingRules.ResetDynamicState();
            var registry = new RomanGensRegistry(RomanGensData.All);
            var random = new System.Random(seed);
            var builder = new PopulationBuilder(startYear, random);

            foreach (var definition in registry.Definitions)
            {
                if (definition == null)
                    continue;

                foreach (var variant in definition.Variants)
                {
                    if (variant == null)
                        continue;

                    EnsureVariantBranches(variant, random);
                    var branches = variant.GetBranches().ToList();
                    foreach (var branch in branches)
                    {
                        if (branch == null)
                            continue;

                        GenerateBranchFamily(builder, variant, branch, random);
                    }
                }
            }

            return new CharacterDataWrapper
            {
                Characters = builder.Build()
            };
        }

        private static void EnsureVariantBranches(RomanGensVariant variant, System.Random random)
        {
            if (variant.HasAnyCognomen)
                return;

            int fallbackBranches = 2;
            for (int i = 0; i < fallbackBranches; i++)
                variant.CreateNewBranch(random, null);
        }

        private void GenerateBranchFamily(PopulationBuilder builder, RomanGensVariant variant, RomanCognomenBranch baseBranch, System.Random random)
        {
            var founderMale = builder.CreateAdultForBranch(variant, baseBranch, Gender.Male, 38, 65, generation: 1);
            var founderFemale = builder.CreateAdultForBranch(variant, baseBranch, Gender.Female, 25, 55, generation: 1);
            builder.LinkSpouses(founderMale, founderFemale);

            var gen2Couples = new List<(Character Male, Character Female)>();
            int gen2Count = random.Next(2, 5); // 2-4 adult children
            for (int i = 0; i < gen2Count; i++)
            {
                var child = builder.CreateChildFromParents(variant, founderMale, founderFemale, 18, 55, generation: 2);
                Character malePartner;
                Character femalePartner;
                if (child.Gender == Gender.Male)
                {
                    var branch = variant.GetBranch(child.RomanName?.Cognomen) ?? baseBranch;
                    var spouse = builder.CreateAdultForBranch(variant, branch, Gender.Female, 18, 50, generation: 2);
                    builder.LinkSpouses(child, spouse);
                    malePartner = child;
                    femalePartner = spouse;
                }
                else
                {
                    var branch = variant.GetBranch(child.RomanName?.Cognomen) ?? baseBranch;
                    var spouse = builder.CreateAdultForBranch(variant, branch, Gender.Male, 20, 55, generation: 2);
                    builder.LinkSpouses(child, spouse);
                    malePartner = spouse;
                    femalePartner = child;
                }

                gen2Couples.Add((malePartner, femalePartner));
            }

            var generation3 = new List<Character>();
            foreach (var couple in gen2Couples)
            {
                int childCount = random.Next(2, 4); // 2-3 children per Gen2 couple
                for (int i = 0; i < childCount; i++)
                {
                    var child = builder.CreateChildFromParents(variant, couple.Male, couple.Female, 0, 30, generation: 3);
                    generation3.Add(child);
                }
            }

            var eligible = generation3
                .Where(c => c.Age >= 18)
                .OrderBy(_ => random.Next())
                .ToList();

            int marriageRate = random.Next(30, 51);
            int marriages = Math.Min(eligible.Count, (eligible.Count * marriageRate) / 100);
            for (int i = 0; i < marriages; i++)
            {
                var partner = eligible[i];
                var partnerBranch = variant.GetBranch(partner.RomanName?.Cognomen) ?? baseBranch;
                if (partner.Gender == Gender.Male)
                {
                    var spouse = builder.CreateAdultForBranch(variant, partnerBranch, Gender.Female, 18, 45, generation: 3);
                    builder.LinkSpouses(partner, spouse);
                    GenerateGenerationFour(builder, variant, partner, spouse, random);
                }
                else
                {
                    var spouse = builder.CreateAdultForBranch(variant, partnerBranch, Gender.Male, 20, 45, generation: 3);
                    builder.LinkSpouses(partner, spouse);
                    GenerateGenerationFour(builder, variant, spouse, partner, random);
                }
            }
        }

        private void GenerateGenerationFour(PopulationBuilder builder, RomanGensVariant variant, Character father, Character mother, System.Random random)
        {
            int count = random.Next(2, 5); // 2-4 children
            for (int i = 0; i < count; i++)
            {
                builder.CreateChildFromParents(variant, father, mother, 0, 12, generation: 4);
            }
        }
    }

    internal sealed class PopulationBuilder
    {
        private readonly int startYear;
        private readonly System.Random random;
        private readonly List<Character> characters = new();
        private int nextId = 1;

        public PopulationBuilder(int startYear, System.Random random)
        {
            this.startYear = startYear;
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public List<Character> Build() => characters;

        public Character CreateAdultForBranch(RomanGensVariant variant, RomanCognomenBranch branch, Gender gender, int minAge, int maxAge, int generation)
        {
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));

            return CreateCharacter(variant, branch, gender, minAge, maxAge, generation, null, null);
        }

        public Character CreateChildFromParents(RomanGensVariant variant, Character father, Character mother, int minAge, int maxAge, int generation)
        {
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));

            return CreateCharacter(variant, null, NextGender(), minAge, maxAge, generation, father, mother);
        }

        public void LinkSpouses(Character a, Character b)
        {
            if (a != null)
                a.SpouseID = b?.ID;
            if (b != null)
                b.SpouseID = a?.ID;
        }

        public Gender NextGender() => random.Next(0, 2) == 0 ? Gender.Male : Gender.Female;

        private Character CreateCharacter(
            RomanGensVariant variant,
            RomanCognomenBranch branch,
            Gender gender,
            int minAge,
            int maxAge,
            int generation,
            Character father,
            Character mother)
        {
            int earliestBirth = startYear - maxAge;
            int latestBirth = startYear - minAge;
            if (earliestBirth > latestBirth)
                earliestBirth = latestBirth;

            int desiredAge = random.Next(minAge, maxAge + 1);
            int birthYear = startYear - desiredAge;
            birthYear = Clamp(birthYear, earliestBirth, latestBirth);

            if (father != null)
                birthYear = Math.Max(birthYear, father.BirthYear + 16);
            if (mother != null)
                birthYear = Math.Max(birthYear, mother.BirthYear + 16);

            birthYear = Math.Min(birthYear, latestBirth);
            birthYear = Math.Max(birthYear, earliestBirth);
            birthYear = Math.Min(birthYear, startYear);

            int age = Math.Max(0, startYear - birthYear);
            if (age > maxAge)
                age = maxAge;

            int birthMonth = random.Next(1, 13);
            int birthDay = random.Next(1, 29);

            RomanName romanName;
            if (father != null || mother != null)
            {
                romanName = RomanNamingRules.GenerateChildName(father, mother, gender, variant.SocialClass, random);
            }
            else
            {
                romanName = RomanNamingRules.GenerateNameForBranch(gender, variant, branch, random);
            }

            var familySeed = father?.Family ?? mother?.Family ?? variant.Definition.StylizedNomen;
            var family = RomanNamingRules.ResolveFamilyName(familySeed, romanName) ?? variant.Definition.StylizedNomen;

            var status = GenerateStatusValues(variant.SocialClass, generation);

            var character = new Character
            {
                ID = nextId++,
                RomanName = romanName,
                Gender = gender,
                BirthYear = birthYear,
                BirthMonth = birthMonth,
                BirthDay = birthDay,
                Age = age,
                IsAlive = true,
                FatherID = father?.ID,
                MotherID = mother?.ID,
                Family = family,
                Branch = romanName?.BranchId,
                Class = variant.SocialClass,
                Traits = new List<string>(),
                TraitRecords = new List<TraitRecord>(),
                Wealth = status.wealth,
                Influence = status.influence,
                Ambition = null
            };

            characters.Add(character);
            return character;
        }

        private (int wealth, int influence) GenerateStatusValues(SocialClass socialClass, int generation)
        {
            int wealthBase = socialClass switch
            {
                SocialClass.Patrician => 4500,
                SocialClass.Equestrian => 3200,
                _ => 1300
            };

            int influenceBase = socialClass switch
            {
                SocialClass.Patrician => 6,
                SocialClass.Equestrian => 4,
                _ => 3
            };

            switch (generation)
            {
                case 1:
                    wealthBase += random.Next(1500, 4000);
                    influenceBase += random.Next(2, 5);
                    break;
                case 2:
                    wealthBase += random.Next(600, 1800);
                    influenceBase += random.Next(1, 3);
                    break;
                case 3:
                    wealthBase = Math.Max(wealthBase / 2, 300) + random.Next(200, 900);
                    influenceBase = Math.Max(1, influenceBase - 1 + random.Next(0, 3));
                    break;
                default:
                    wealthBase = Math.Max(wealthBase / 3, 150) + random.Next(100, 600);
                    influenceBase = Math.Max(0, influenceBase - 2 + random.Next(0, 2));
                    break;
            }

            return (Math.Max(50, wealthBase), Math.Max(0, influenceBase));
        }

        private int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
