using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Characters
{
    /// <summary>
    /// Core data definition for all characters in the simulation.
    /// </summary>
    [Serializable]
    public class Character
    {
        private const int DefaultStatValue = 5;

        // ----------------------------------------------------------------------
        // Identity
        // ----------------------------------------------------------------------
        public int ID;
        public RomanName RomanName;  // ✅ unified structured name
        public Gender Gender;

        // ----------------------------------------------------------------------
        // Birth & Lifecycle
        // ----------------------------------------------------------------------
        public int BirthYear;
        public int BirthMonth;
        public int BirthDay;
        public int Age;
        public bool IsAlive = true;

        // ----------------------------------------------------------------------
        // Relationships
        // ----------------------------------------------------------------------
        public int? SpouseID;
        public int? FatherID;
        public int? MotherID;
        public int? SiblingID;

        // ----------------------------------------------------------------------
        // Social & Cultural Data
        // ----------------------------------------------------------------------
        public string Family; // gens
        public SocialClass Class;
        public List<string> Traits = new List<string>();
        public List<TraitRecord> TraitRecords = new List<TraitRecord>();
        public int Wealth;
        public int Influence;
        public float SenatorialInfluence;
        public float PopularInfluence;
        public float MilitaryInfluence;
        public float FamilyInfluence;
        public int Oratory = DefaultStatValue;
        public int AmbitionScore = DefaultStatValue;
        public int Courage = DefaultStatValue;
        public int Dignitas = DefaultStatValue;
        public int Administration = DefaultStatValue;
        public int Judgment = DefaultStatValue;
        public int Strategy = DefaultStatValue;
        public int Civic = DefaultStatValue;
        public FactionType Faction = FactionType.Neutral;
        public AmbitionProfile Ambition = new AmbitionProfile();
        public List<CareerMilestone> CareerMilestones = new List<CareerMilestone>();
        public OfficeAssignment CurrentOffice;
        public List<OfficeHistoryEntry> OfficeHistory = new List<OfficeHistoryEntry>();

        // ----------------------------------------------------------------------
        // Helper Methods
        // ----------------------------------------------------------------------
        public string FullName => RomanName?.GetFullName() ?? string.Empty;
        public bool IsMale => Gender == Gender.Male;
        public bool IsFemale => Gender == Gender.Female;
        public bool IsAdult => Age >= 15;

        public void AgeUp() => Age++;

        public int AgeAt(int currentYear) => currentYear - BirthYear;

        public float GetTotalInfluence()
        {
            return SenatorialInfluence + PopularInfluence + MilitaryInfluence + FamilyInfluence;
        }

        public PoliticalProfile GetPoliticalProfile()
        {
            return PoliticalProfile.FromCharacter(this);
        }

        public PoliticalBehaviorModel GetPoliticalBehavior()
        {
            return PoliticalBehaviorModel.FromProfile(GetPoliticalProfile());
        }

        public CharacterPoliticalSummary GetPoliticalSummary()
        {
            var profile = GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            return CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);
        }

        public bool IsAlignedWith(FactionType faction)
        {
            return Faction == faction;
        }

        public void ClearCurrentOffice()
        {
            CurrentOffice = default;
        }

        public Character Clone()
        {
            return new Character
            {
                ID = ID,
                RomanName = RomanName != null
                    ? new RomanName(RomanName.Praenomen, RomanName.Nomen, RomanName.Cognomen, RomanName.Gender)
                    : null,
                Gender = Gender,
                BirthYear = BirthYear,
                BirthMonth = BirthMonth,
                BirthDay = BirthDay,
                Age = Age,
                IsAlive = IsAlive,
                SpouseID = SpouseID,
                FatherID = FatherID,
                MotherID = MotherID,
                Family = Family,
                Class = Class,
                Traits = new List<string>(Traits),
                TraitRecords = CloneTraitRecords(),
                Ambition = Ambition?.Clone(),
                CareerMilestones = CloneCareerMilestones(),
                Wealth = Wealth,
                Influence = Influence,
                SenatorialInfluence = SenatorialInfluence,
                PopularInfluence = PopularInfluence,
                MilitaryInfluence = MilitaryInfluence,
                FamilyInfluence = FamilyInfluence,
                Oratory = Oratory,
                AmbitionScore = AmbitionScore,
                Courage = Courage,
                Dignitas = Dignitas,
                Administration = Administration,
                Judgment = Judgment,
                Strategy = Strategy,
                Civic = Civic,
                Faction = Faction,
                CurrentOffice = CurrentOffice,
                OfficeHistory = CloneOfficeHistory()
            };
        }

        private List<TraitRecord> CloneTraitRecords()
        {
            if (TraitRecords == null)
                return new List<TraitRecord>();

            var clone = new List<TraitRecord>(TraitRecords.Count);
            foreach (var record in TraitRecords)
            {
                clone.Add(record?.Clone());
            }

            return clone;
        }

        private List<CareerMilestone> CloneCareerMilestones()
        {
            if (CareerMilestones == null)
                return new List<CareerMilestone>();

            var clone = new List<CareerMilestone>(CareerMilestones.Count);
            foreach (var milestone in CareerMilestones)
            {
                clone.Add(milestone?.Clone());
            }

            return clone;
        }

        private List<OfficeHistoryEntry> CloneOfficeHistory()
        {
            if (OfficeHistory == null)
                return new List<OfficeHistoryEntry>();

            var clone = new List<OfficeHistoryEntry>(OfficeHistory.Count);
            foreach (var entry in OfficeHistory)
            {
                clone.Add(entry?.Clone());
            }

            return clone;
        }
    }

    [Serializable]
    public class TraitRecord
    {
        public string Id;
        public int Level = 1;
        public float Experience;
        public int AcquiredYear;

        public TraitRecord Clone()
        {
            return new TraitRecord
            {
                Id = Id,
                Level = Level,
                Experience = Experience,
                AcquiredYear = AcquiredYear
            };
        }
    }

    [Serializable]
    public class AmbitionHistoryRecord
    {
        public int Year;
        public string Description;
        public string Outcome;

        public AmbitionHistoryRecord Clone()
        {
            return new AmbitionHistoryRecord
            {
                Year = Year,
                Description = Description,
                Outcome = Outcome
            };
        }
    }

    [Serializable]
    public class AmbitionProfile
    {
        public string CurrentGoal;
        public int Intensity;
        public int? TargetYear;
        public bool IsRetired;
        public int LastEvaluatedYear;
            public List<AmbitionHistoryRecord> History = new List<AmbitionHistoryRecord>();

        public AmbitionProfile Clone()
        {
            return new AmbitionProfile
            {
                CurrentGoal = CurrentGoal,
                Intensity = Intensity,
                TargetYear = TargetYear,
                IsRetired = IsRetired,
                LastEvaluatedYear = LastEvaluatedYear,
                History = CloneHistory()
            };
        }

        public static AmbitionProfile CreateDefault(Character character = null)
        {
            var profile = new AmbitionProfile();
            profile.CurrentGoal = InferDefaultGoal(character);
            profile.Intensity = InferDefaultIntensity(character);
            profile.TargetYear = character != null ? character.BirthYear + Math.Max(30, character.Age + 5) : null;
            profile.LastEvaluatedYear = character != null ? character.BirthYear + character.Age : 0;
            return profile;
        }

        public static string InferDefaultGoal(Character character)
        {
            if (character == null)
                return "Maintain Standing";

            return character.Class switch
            {
                SocialClass.Patrician => "Consulship",
                SocialClass.Equestrian => "Praetorship",
                _ => "Tribunate"
            };
        }

        public static int InferDefaultIntensity(Character character)
        {
            if (character == null)
                return 35;

            int baseValue = character.Class switch
            {
                SocialClass.Patrician => 60,
                SocialClass.Equestrian => 50,
                _ => 40
            };

            if (character.Age < 25)
                baseValue += 10;
            else if (character.Age < 35)
                baseValue += 5;
            else if (character.Age > 55)
                baseValue -= 10;

            return Mathf.Clamp(baseValue, 15, 80);
        }

        private List<AmbitionHistoryRecord> CloneHistory()
        {
            if (History == null)
                return new List<AmbitionHistoryRecord>();

            var clone = new List<AmbitionHistoryRecord>(History.Count);
            foreach (var entry in History)
            {
                clone.Add(entry?.Clone());
            }

            return clone;
        }
    }

    [Serializable]
    public class CareerMilestone
    {
        public string Title;
        public int Year;
        public string Notes;

        public CareerMilestone Clone()
        {
            return new CareerMilestone
            {
                Title = Title,
                Year = Year,
                Notes = Notes
            };
        }
    }

    [Serializable]
    public class OfficeHistoryEntry
    {
        public string OfficeId;
        public int SeatIndex;
        public int StartYear;
        public int? EndYear;
        public string Notes;

        public OfficeHistoryEntry Clone()
        {
            return new OfficeHistoryEntry
            {
                OfficeId = OfficeId,
                SeatIndex = SeatIndex,
                StartYear = StartYear,
                EndYear = EndYear,
                Notes = Notes
            };
        }
    }

    [Serializable]
    public struct OfficeAssignment
    {
        public string OfficeId;
        public int SeatIndex;
        public int StartYear;

        public bool IsEmpty => string.IsNullOrEmpty(OfficeId);
    }

    [Serializable]
    public class CharacterDataWrapper
    {
        public List<Character> Characters = new List<Character>();
    }

    public enum Gender
    {
        Male,
        Female
    }

    public enum SocialClass
    {
        Patrician,
        Plebeian,
        Equestrian
    }

    public enum FactionType
    {
        Neutral = 0,
        Optimates = 1,
        Populares = 2,
        Militarists = 3,
        Moderates = 4
    }
}
