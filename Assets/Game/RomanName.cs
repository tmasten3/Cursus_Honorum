using System;
using System.Linq;
using UnityEngine;

namespace Game.Data.Characters
{
    [Serializable]
    public class RomanName
    {
        public string Praenomen;
        public string Nomen;
        public string Cognomen;
        public Gender Gender;

        public RomanName(string praenomen, string nomen, string cognomen, Gender gender)
        {
            Praenomen = praenomen;
            Nomen = nomen;
            Cognomen = cognomen;
            Gender = gender;
        }

        public string GetFullName()
        {
            if (Gender == Gender.Male)
                return string.Join(" ", new[] { Praenomen, Nomen, Cognomen }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            else
                return string.Join(" ", new[] { Nomen }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
        }
    }
}
