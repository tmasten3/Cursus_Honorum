using System;
using System.Linq;
using UnityEngine;

namespace Game.Data.Characters
{
    public static class RomanNamingRules
    {
        private static readonly System.Random rng = new();

        private static readonly string[] MalePraenomina =
        {
            "Gaius", "Lucius", "Marcus", "Publius", "Quintus",
            "Tiberius", "Aulus", "Sextus", "Servius", "Spurius"
        };

        private static readonly string[] PatricianNomina =
        {
            "Cornelius", "Claudius", "Fabius", "Aemilius", "Julius", "Valerius"
        };

        private static readonly string[] PlebeianNomina =
        {
            "Aurelius", "Sempronius", "Sulpicius", "Licinius", "Servilius"
        };

        private static readonly string[] EquestrianNomina =
        {
            "Antonius", "Flavius", "Caecilius", "Domitius", "Pompeius"
        };

        private static readonly string[] Cognomina =
        {
            "Scipio", "Nero", "Pulcher", "Lentulus", "Paullus",
            "Caesar", "Cato", "Gracchus", "Crassus", "Metellus"
        };

        // --------------------------------------------------------------

        public static RomanName GenerateRomanName(Gender gender, string gens = null, SocialClass socialClass = SocialClass.Patrician)
        {
            if (gender == Gender.Male)
            {
                string praenomen = GetPraenomen();
                string nomen = gens ?? GetNomen(socialClass);
                string cognomen = GetCognomen();
                return new RomanName(praenomen, nomen, cognomen, gender);
            }
            else
            {
                string gensName = gens ?? GetNomen(socialClass);
                string feminine = GetFeminineForm(gensName);
                return new RomanName(null, feminine, null, gender);
            }
        }

        public static string GetFullName(Character c)
        {
            if (c?.RomanName == null)
                return "Unknown";

            var n = c.RomanName;

            if (c.IsFemale)
                return n.Nomen ?? "Unknown";

            return string.Join(" ", new[] { n.Praenomen, n.Nomen, n.Cognomen }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        public static string GetFeminineForm(string gens)
        {
            if (string.IsNullOrEmpty(gens))
                return "Unknown";

            if (gens.EndsWith("ius"))
                return gens[..^3] + "ia";
            if (gens.EndsWith("us"))
                return gens[..^2] + "a";
            if (gens.EndsWith("as"))
                return gens[..^2] + "a";
            return gens + "a";
        }

        public static string GetPraenomen() =>
            MalePraenomina[rng.Next(MalePraenomina.Length)];

        public static string GetNomen(SocialClass socialClass)
        {
            string[] pool = socialClass switch
            {
                SocialClass.Patrician => PatricianNomina,
                SocialClass.Plebeian => PlebeianNomina,
                SocialClass.Equestrian => EquestrianNomina,
                _ => PatricianNomina
            };
            return pool[rng.Next(pool.Length)];
        }

        public static string GetCognomen() =>
            Cognomina[rng.Next(Cognomina.Length)];
    }
}
