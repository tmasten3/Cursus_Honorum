using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Data.Characters.Generation
{
    internal sealed class BasePopulationGenerator
    {
        private readonly RomanNamingService namingService;
        private readonly int startYear;
        private readonly Random random;
        private readonly PopulationIndex index = new PopulationIndex();
        private readonly List<Character> characters = new List<Character>();
        private readonly Dictionary<int, Character> byId = new Dictionary<int, Character>();
        private int nextId = 1;

        public BasePopulationGenerator(RomanNamingService namingService, int seed, int startYear)
        {
            this.namingService = namingService ?? throw new ArgumentNullException(nameof(namingService));
            this.startYear = startYear;
            random = new Random(seed);
        }

        public BasePopulationGeneratorResult Generate()
        {
            var orderedBranches = namingService.GetAllBranches()
                .OrderBy(b => b.SocialClass)
                .ThenBy(b => b.StylizedNomen)
                .ThenBy(b => b.Cognomen)
                .ToList();

            foreach (var branch in orderedBranches)
            {
                GenerateBranchPopulation(branch);
            }

            while (characters.Count < 1200)
            {
                var supplemental = namingService.CreateSupplementalBranch(random);
                if (supplemental == null)
                    break;
                GenerateBranchPopulation(supplemental);
            }

            var wrapper = new CharacterDataWrapper { Characters = characters };
            return new BasePopulationGeneratorResult(wrapper, index);
        }

        private void GenerateBranchPopulation(RomanFamilyBranch branch)
        {
            if (branch == null)
                return;

            int founderId = nextId;
            string lineageKey = $"{branch.Id}:F{founderId}";
            var paterAge = random.Next(45, 66);
            var pater = CreateAdult(branch, Gender.Male, paterAge, lineageKey, null, null, generationDepth: 0);

            var materAge = Math.Max(30, paterAge - random.Next(4, 11));
            var mater = CreateAdult(branch, Gender.Female, materAge, lineageKey, null, null, generationDepth: 0);
            LinkSpouses(pater, mater);

            var gen2 = new List<Character>();
            int gen2Count = random.Next(2, 4);
            for (int i = 0; i < gen2Count; i++)
            {
                var gender = random.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
                int maxAge = Math.Max(18, pater.Age - 18);
                int childAge = random.Next(18, Math.Min(maxAge, 46));
                var child = CreateChildCharacter(pater, mater, gender, childAge, lineageKey, generationDepth: 1);
                gen2.Add(child);
            }

            foreach (var child in gen2)
            {
                CreateSpouseFor(child, branch, generationDepth: 1);
            }

            var gen3 = new List<Character>();
            foreach (var child in gen2)
            {
                var spouse = GetSpouse(child);
                if (spouse == null)
                    continue;

                int childrenCount = random.Next(2, 4);
                for (int i = 0; i < childrenCount; i++)
                {
                    int maxAge = Math.Max(0, Math.Min(child.Age - 18, 30));
                    int age = random.Next(0, Math.Max(1, maxAge + 1));
                    var gender = random.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
                    var offspring = CreateChildCharacter(child, spouse, gender, age, child.LineageKey, generationDepth: 2);
                    gen3.Add(offspring);
                }
            }

            var gen3Adults = gen3.Where(c => c.Age >= 18).ToList();
            Shuffle(gen3Adults);
            int marriageTargets = (int)Math.Round(gen3Adults.Count * (0.30 + random.NextDouble() * 0.20));
            marriageTargets = Math.Min(marriageTargets, gen3Adults.Count);

            for (int i = 0; i < marriageTargets; i++)
            {
                var adult = gen3Adults[i];
                if (adult.SpouseID.HasValue)
                    continue;

                var spouse = CreateSpouseFor(adult, branch, generationDepth: 2);
                if (spouse == null)
                    continue;

                int childrenCount = random.Next(2, 5);
                for (int c = 0; c < childrenCount; c++)
                {
                    int age = random.Next(0, 13);
                    var gender = random.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
                    CreateChildCharacter(adult, spouse, gender, age, adult.LineageKey, generationDepth: 3);
                }
            }
        }

        private Character CreateAdult(RomanFamilyBranch branch, Gender gender, int age, string lineageKey, int? fatherId, int? motherId, int generationDepth)
        {
            var identity = namingService.GenerateIdentity(gender, branch.SocialClass, branch.StylizedNomen, branch.Id, random);
            return CreateCharacterFromIdentity(identity, branch, gender, age, lineageKey, fatherId, motherId, generationDepth);
        }

        private Character CreateSpouseFor(Character partner, RomanFamilyBranch branch, int generationDepth)
        {
            if (partner == null)
                return null;

            var spouseGender = partner.Gender == Gender.Male ? Gender.Female : Gender.Male;
            int age = Math.Clamp(partner.Age + random.Next(-5, 6), 16, 65);
            var identity = namingService.GenerateIdentity(spouseGender, branch.SocialClass, branch.StylizedNomen, branch.Id, random);
            var spouse = CreateCharacterFromIdentity(identity, identity.Branch ?? branch, spouseGender, age, partner.LineageKey, null, null, generationDepth);
            LinkSpouses(partner, spouse);
            return spouse;
        }

        private Character CreateChildCharacter(Character father, Character mother, Gender gender, int age, string lineageKey, int generationDepth)
        {
            var socialClass = father?.Class ?? mother?.Class ?? SocialClass.Plebeian;
            var identity = namingService.GenerateChildIdentity(father, mother, gender, socialClass, random);
            var branch = identity.Branch;
            var child = CreateCharacterFromIdentity(identity, branch, gender, age, lineageKey, father?.ID, mother?.ID, generationDepth);

            if (father != null)
                father.Children.Add(child.ID);
            if (mother != null)
                mother.Children.Add(child.ID);

            return child;
        }

        private Character CreateCharacterFromIdentity(
            RomanNamingResult identity,
            RomanFamilyBranch branch,
            Gender gender,
            int age,
            string lineageKey,
            int? fatherId,
            int? motherId,
            int generationDepth)
        {
            var name = identity?.Name;
            var resolvedBranch = identity?.Branch ?? branch;

            var character = new Character
            {
                ID = nextId++,
                RomanName = name,
                Gender = gender,
                BirthYear = startYear - age,
                BirthMonth = random.Next(1, 13),
                BirthDay = random.Next(1, 29),
                Age = age,
                IsAlive = true,
                FatherID = fatherId,
                MotherID = motherId,
                Family = resolvedBranch?.StylizedNomen ?? RomanNameUtility.Normalize(name?.Nomen),
                Class = resolvedBranch?.SocialClass ?? SocialClass.Plebeian,
                BranchId = resolvedBranch?.Id,
                LineageKey = lineageKey,
                Children = new List<int>(),
                Traits = new List<string>(),
                TraitRecords = new List<TraitRecord>(),
                CareerMilestones = new List<CareerMilestone>(),
                OfficeHistory = new List<OfficeHistoryEntry>()
            };

            if (character.RomanName != null)
                character.RomanName.Gender = gender;

            if (string.IsNullOrEmpty(character.LineageKey))
            {
                if (fatherId.HasValue && fatherId.Value != 0 && byId.TryGetValue(fatherId.Value, out var father) && father != null && string.Equals(father.BranchId, character.BranchId, StringComparison.OrdinalIgnoreCase))
                    character.LineageKey = father.LineageKey;
                else if (motherId.HasValue && motherId.Value != 0 && byId.TryGetValue(motherId.Value, out var mother) && mother != null && string.Equals(mother.BranchId, character.BranchId, StringComparison.OrdinalIgnoreCase))
                    character.LineageKey = mother.LineageKey;
                else if (!string.IsNullOrEmpty(character.BranchId))
                    character.LineageKey = $"{character.BranchId}:F{character.ID}";
            }

            ApplyAttributes(character, generationDepth);
            RegisterCharacter(character);
            return character;
        }

        private void ApplyAttributes(Character character, int generationDepth)
        {
            int baseWealthMin = character.Class == SocialClass.Patrician ? 3200 : 800;
            int baseWealthMax = character.Class == SocialClass.Patrician ? 7800 : 2600;
            int penalty = generationDepth * 400;
            character.Wealth = random.Next(Math.Max(200, baseWealthMin - penalty), Math.Max(baseWealthMin, baseWealthMax - penalty));

            float senatorialBase = character.Class == SocialClass.Patrician ? 6f : 3f;
            float popularBase = 2f + generationDepth * 0.3f;
            float militaryBase = 1.5f + generationDepth * 0.2f;
            float familyBase = 2f + Math.Max(0, 4 - generationDepth);

            character.SenatorialInfluence = Math.Max(0f, senatorialBase + (float)random.NextDouble() * 2f - generationDepth * 0.4f);
            character.PopularInfluence = Math.Max(0f, popularBase + (float)random.NextDouble() * 1.5f);
            character.MilitaryInfluence = Math.Max(0f, militaryBase + (float)random.NextDouble() * 1.2f);
            character.FamilyInfluence = Math.Max(0f, familyBase + (float)random.NextDouble());

            character.Oratory = ClampStat(5 + random.Next(-2, 3));
            character.AmbitionScore = ClampStat(6 + random.Next(-2, 3));
            character.Courage = ClampStat(5 + random.Next(-2, 3));
            character.Dignitas = ClampStat(6 + random.Next(-2, 3));
            character.Administration = ClampStat(5 + random.Next(-2, 3));
            character.Judgment = ClampStat(5 + random.Next(-2, 3));
            character.Strategy = ClampStat(5 + random.Next(-2, 3));
            character.Civic = ClampStat(5 + random.Next(-2, 3));

            var factions = Enum.GetValues(typeof(FactionType));
            character.Faction = (FactionType)factions.GetValue(random.Next(factions.Length));
        }

        private static int ClampStat(int value) => Math.Clamp(value, 1, 12);

        private void RegisterCharacter(Character character)
        {
            characters.Add(character);
            byId[character.ID] = character;
            index.Register(character);
        }

        private void LinkSpouses(Character a, Character b)
        {
            if (a == null || b == null)
                return;

            a.SpouseID = b.ID;
            b.SpouseID = a.ID;
        }

        private Character GetSpouse(Character character)
        {
            if (character?.SpouseID == null)
                return null;
            return byId.TryGetValue(character.SpouseID.Value, out var spouse) ? spouse : null;
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
