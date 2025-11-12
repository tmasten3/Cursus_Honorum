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
        public List<string> Traits = new();
        public int Wealth;
        public int Influence;

        // ----------------------------------------------------------------------
        // Helper Methods
        // ----------------------------------------------------------------------
        public string FullName => RomanName?.GetFullName() ?? "Unknown";
        public bool IsMale => Gender == Gender.Male;
        public bool IsFemale => Gender == Gender.Female;
        public bool IsAdult => Age >= 15;

        public void AgeUp() => Age++;

        public int AgeAt(int currentYear) => currentYear - BirthYear;

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
                Wealth = Wealth,
                Influence = Influence
            };
        }
    }

    [Serializable]
    public class CharacterDataWrapper
    {
        public List<Character> Characters = new();
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
}
