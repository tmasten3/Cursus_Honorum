using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Characters;

namespace Game.Systems.Characters.Generation
{
    /// <summary>
    /// Procedurally generates the initial population using Roman gens/branch data.
    /// </summary>
    internal sealed class BaseCharactersGenerator
    {
        private const int MinimumMarriageAge = 16;

        private readonly int startYear;
        private readonly Random random;

        private readonly List<Character> characters = new List<Character>();
        private readonly Dictionary<int, Character> byId = new Dictionary<int, Character>();
        private int nextId = 1;

        public BaseCharactersGenerator(int seed, int startYear)
        {
            this.startYear = startYear;
            random = new Random(seed);
        }

        public List<Character> Generate()
        {
            characters.Clear();
            byId.Clear();
            nextId = 1;

            var plans = BuildBranchPlans().ToList();

            foreach (var plan in plans)
                GenerateBranchPopulation(plan);

            return characters;
        }

        private IEnumerable<BranchPlan> BuildBranchPlans()
        {
            var plans = new List<BranchPlan>();

            foreach (var definition in RomanGensData.All)
            {
                foreach (var variant in definition.Variants)
                {
                    var baseCognomina = variant.BaseCognomina;
                    if (baseCognomina.Count > 0)
                    {
                        foreach (var cognomen in baseCognomina)
                        {
                            var branch = RomanFamilyRegistry.RegisterOrGet(definition.StylizedNomen, variant.SocialClass, cognomen, null, false);
                            plans.Add(new BranchPlan(variant, branch));
                        }
                    }
                    else
                    {
                        int branchCount = random.Next(2, 4);
                        for (int i = 0; i < branchCount; i++)
                        {
                            var generated = RomanNamingRules.GenerateDynamicCognomen(variant, random, null);
                            variant.RegisterCognomen(generated);
                            var branch = RomanFamilyRegistry.RegisterOrGet(definition.StylizedNomen, variant.SocialClass, generated, null, true);
                            plans.Add(new BranchPlan(variant, branch));
                        }
                    }
                }
            }

            Shuffle(plans);
            return plans;
        }

        private void GenerateBranchPopulation(BranchPlan plan)
        {
            var founders = CreateFounders(plan);
            var gen2Couples = CreateSecondGeneration(plan, founders);
            var gen3Marriages = CreateThirdGeneration(plan, gen2Couples);
            GenerateFourthGeneration(gen3Marriages);
        }

        private Couple CreateFounders(BranchPlan plan)
        {
            int maleAge = random.Next(42, 67);
            int femaleAge = Math.Clamp(maleAge - random.Next(4, 13), 26, 55);

            var maleIdentity = CreateFounderIdentity(plan, Gender.Male);
            var femaleIdentity = CreateFounderIdentity(plan, Gender.Female);

            var husband = CreateCharacter(maleIdentity, Gender.Male, startYear - maleAge, plan.SocialClass);
            var wife = CreateCharacter(femaleIdentity, Gender.Female, startYear - femaleAge, plan.SocialClass);

            LinkMarriage(husband, wife);

            return new Couple(husband, wife);
        }

        private RomanIdentity CreateFounderIdentity(BranchPlan plan, Gender gender)
        {
            string gens = plan.Branch.GensKey;
            string cognomen = plan.Branch.Cognomen;
            string nomen = gender == Gender.Male
                ? gens
                : RomanNamingRules.GetFeminineForm(gens);

            var template = new RomanName(null, nomen, cognomen, gender);
            return RomanNamingRules.NormalizeOrGenerateIdentity(
                gender,
                plan.SocialClass,
                gens,
                template,
                null,
                random);
        }

        private List<Couple> CreateSecondGeneration(BranchPlan plan, Couple founders)
        {
            int childCount = random.Next(2, 5);
            var couples = new List<Couple>();
            var children = new List<Character>();

            for (int i = 0; i < childCount; i++)
            {
                var gender = random.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
                int age = GenerateAdultAge(founders.Male.Age, founders.Female.Age);
                int birthYear = startYear - age;
                var identity = RomanNamingRules.GenerateChildIdentity(founders.Male, founders.Female, gender, plan.SocialClass, random);
                var child = CreateCharacter(identity, gender, birthYear, plan.SocialClass, founders.Male, founders.Female);
                children.Add(child);
            }

            foreach (var child in children.OrderByDescending(c => c.Age))
            {
                var spouse = CreateSpouseFor(child);
                LinkMarriage(child, spouse);
                couples.Add(NormalizeCouple(child, spouse));
            }

            return couples;
        }

        private List<Couple> CreateThirdGeneration(BranchPlan plan, List<Couple> gen2Couples)
        {
            var gen3 = new List<Character>();

            foreach (var couple in gen2Couples)
            {
                int childCount = random.Next(2, 5);
                for (int i = 0; i < childCount; i++)
                {
                    var gender = random.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
                    int age = GenerateChildAge(couple.Male, couple.Female);
                    int birthYear = startYear - age;
                    var identity = RomanNamingRules.GenerateChildIdentity(couple.Male, couple.Female, gender, couple.Male.Class, random);
                    var child = CreateCharacter(identity, gender, birthYear, couple.Male.Class, couple.Male, couple.Female);
                    gen3.Add(child);
                }
            }

            var adults = gen3.Where(c => c.Age >= MinimumMarriageAge).ToList();
            Shuffle(adults);

            int marriageTarget = adults.Count == 0 ? 0 : Math.Max(0, (int)Math.Round(adults.Count * random.Next(30, 46) / 100.0));
            var marriages = new List<Couple>();

            for (int i = 0; i < marriageTarget && i < adults.Count; i++)
            {
                var individual = adults[i];
                if (individual.SpouseID.HasValue)
                    continue;

                var spouse = CreateSpouseFor(individual);
                LinkMarriage(individual, spouse);
                marriages.Add(NormalizeCouple(individual, spouse));
            }

            return marriages;
        }

        private void GenerateFourthGeneration(List<Couple> marriages)
        {
            foreach (var couple in marriages)
            {
                int childCount = random.Next(2, 4);
                for (int i = 0; i < childCount; i++)
                {
                    var gender = random.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
                    int age = GenerateYoungChildAge(couple.Male, couple.Female);
                    int birthYear = startYear - age;
                    var identity = RomanNamingRules.GenerateChildIdentity(couple.Male, couple.Female, gender, couple.Male.Class, random);
                    CreateCharacter(identity, gender, birthYear, couple.Male.Class, couple.Male, couple.Female);
                }
            }
        }

        private Character CreateSpouseFor(Character partner)
        {
            var spouseGender = partner.Gender == Gender.Male ? Gender.Female : Gender.Male;
            var identity = RomanNamingRules.GenerateStandaloneIdentity(spouseGender, partner.Class, null, random);
            int age = GenerateSpouseAge(partner.Age);
            int birthYear = startYear - age;
            return CreateCharacter(identity, spouseGender, birthYear, partner.Class);
        }

        private Character CreateCharacter(
            RomanIdentity identity,
            Gender gender,
            int birthYear,
            SocialClass socialClass,
            Character father = null,
            Character mother = null,
            string resolvedFamilyOverride = null)
        {
            int month = random.Next(1, 13);
            int day = random.Next(1, 29);
            int age = Math.Max(0, startYear - birthYear);

            var character = new Character
            {
                ID = nextId++,
                Gender = gender,
                BirthYear = birthYear,
                BirthMonth = month,
                BirthDay = day,
                Age = age,
                IsAlive = true,
                FatherID = father?.ID,
                MotherID = mother?.ID,
                Class = socialClass,
                TraitRecords = new List<TraitRecord>(),
                CareerMilestones = new List<CareerMilestone>(),
                OfficeHistory = new List<OfficeHistoryEntry>()
            };

            CharacterFactory.ApplyIdentity(character, identity, resolvedFamilyOverride);
            character.Ambition = AmbitionProfile.CreateDefault(character);

            AssignEconomicProfile(character);

            characters.Add(character);
            byId[character.ID] = character;
            return character;
        }

        private void AssignEconomicProfile(Character character)
        {
            if (character.Age < MinimumMarriageAge)
            {
                character.Wealth = random.Next(0, 60);
                character.Influence = 0;
                character.SenatorialInfluence = 0f;
                character.PopularInfluence = 0f;
                character.MilitaryInfluence = 0f;
                character.FamilyInfluence = random.Next(0, 6);
                return;
            }

            character.Wealth = character.Class switch
            {
                SocialClass.Patrician => random.Next(1500, 6000),
                SocialClass.Equestrian => random.Next(800, 3200),
                _ => random.Next(120, 1600)
            };

            character.Influence = character.Class switch
            {
                SocialClass.Patrician => random.Next(12, 45),
                SocialClass.Equestrian => random.Next(6, 28),
                _ => random.Next(1, 16)
            };

            float baseInfluence = character.Influence;
            character.SenatorialInfluence = character.Class == SocialClass.Patrician ? baseInfluence * 0.6f : baseInfluence * 0.2f;
            character.PopularInfluence = baseInfluence * 0.3f;
            character.MilitaryInfluence = character.Gender == Gender.Male ? baseInfluence * 0.25f : baseInfluence * 0.1f;
            character.FamilyInfluence = Math.Max(1f, baseInfluence * 0.2f);
        }

        private static void LinkMarriage(Character a, Character b)
        {
            if (a == null || b == null)
                return;

            a.SpouseID = b.ID;
            b.SpouseID = a.ID;
        }

        private Couple NormalizeCouple(Character first, Character second)
        {
            return first.Gender == Gender.Male
                ? new Couple(first, second)
                : new Couple(second, first);
        }

        private int GenerateAdultAge(int fatherAge, int motherAge)
        {
            int parentalAge = Math.Max(fatherAge, motherAge);
            int maxAge = Math.Max(MinimumMarriageAge + 1, parentalAge - 16);
            return random.Next(MinimumMarriageAge, Math.Max(MinimumMarriageAge + 1, maxAge + 1));
        }

        private int GenerateChildAge(Character father, Character mother)
        {
            int parentalAge = Math.Max(father?.Age ?? 30, mother?.Age ?? 28);
            int maxAge = Math.Max(4, parentalAge - 16);
            int age = random.Next(0, maxAge + 1);
            if (maxAge > 18 && random.NextDouble() < 0.35)
                age = random.Next(MinimumMarriageAge, maxAge + 1);
            return Math.Max(0, age);
        }

        private int GenerateYoungChildAge(Character father, Character mother)
        {
            int parentalAge = Math.Max(father.Age, mother.Age);
            int maxAge = Math.Clamp(parentalAge - 18, 0, 12);
            return random.Next(0, maxAge + 1);
        }

        private int GenerateSpouseAge(int partnerAge)
        {
            int min = Math.Max(MinimumMarriageAge, partnerAge - 8);
            int max = Math.Max(min + 1, partnerAge + 8);
            return random.Next(min, max + 1);
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private readonly struct BranchPlan
        {
            public BranchPlan(RomanGensVariant variant, RomanFamilyBranch branch)
            {
                Variant = variant ?? throw new ArgumentNullException(nameof(variant));
                Branch = branch ?? throw new ArgumentNullException(nameof(branch));
            }

            public RomanGensVariant Variant { get; }
            public RomanFamilyBranch Branch { get; }
            public SocialClass SocialClass => Variant.SocialClass;
        }

        private readonly struct Couple
        {
            public Couple(Character male, Character female)
            {
                Male = male;
                Female = female;
            }

            public Character Male { get; }
            public Character Female { get; }
        }
    }
}
