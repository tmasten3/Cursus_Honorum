using System;
using System.Collections.Generic;

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
            IEnumerable<string> segments = Gender == Gender.Male
                ? new[] { Praenomen, Nomen, Cognomen }
                : new[] { Nomen, Cognomen };

            string last = null;
            var filtered = new List<string>();

            foreach (var raw in segments)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var value = raw.Trim();
                if (value.Length == 0)
                    continue;

                if (last != null && string.Equals(last, value, StringComparison.OrdinalIgnoreCase))
                    continue;

                filtered.Add(value);
                last = value;
            }

            return string.Join(" ", filtered);
        }
    }
}
