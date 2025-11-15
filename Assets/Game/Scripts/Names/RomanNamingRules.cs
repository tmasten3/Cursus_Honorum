using System;
using System.Collections.Generic;

namespace Game.Data.Characters
{
    public static class RomanNamingRules
    {
        private static readonly object Sync = new();
        private static RomanNamingService activeService = new(new RomanNamingContext(1337));

        public static RomanNamingService ActiveService
        {
            get
            {
                lock (Sync)
                {
                    return activeService;
                }
            }
        }

        public static void ConfigureSeed(int seed)
        {
            lock (Sync)
            {
                activeService = new RomanNamingService(new RomanNamingContext(seed));
            }
        }

        public static RomanNamingService CreateIsolatedService(int seed) => new RomanNamingService(new RomanNamingContext(seed));

        public static T WithTemporaryService<T>(RomanNamingService service, Func<T> action)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            lock (Sync)
            {
                var previous = activeService;
                activeService = service;
                try
                {
                    return action();
                }
                finally
                {
                    activeService = previous;
                }
            }
        }

        public static void WithTemporaryService(RomanNamingService service, Action action)
        {
            _ = WithTemporaryService(service, () =>
            {
                action();
                return true;
            });
        }

        public static RomanNamingResult GenerateIdentity(
            Gender gender,
            SocialClass socialClass,
            string gens = null,
            string branchId = null,
            Random randomOverride = null)
        {
            return ActiveService.GenerateIdentity(gender, socialClass, gens, branchId, randomOverride);
        }

        public static RomanNamingResult GenerateStandaloneIdentity(
            Gender gender,
            SocialClass socialClass,
            string gens = null,
            string branchId = null,
            Random randomOverride = null)
        {
            return GenerateIdentity(gender, socialClass, gens, branchId, randomOverride);
        }

        public static RomanNamingResult GenerateChildIdentity(
            Character father,
            Character mother,
            Gender gender,
            SocialClass socialClass,
            Random randomOverride = null)
        {
            return ActiveService.GenerateChildIdentity(father, mother, gender, socialClass, randomOverride);
        }

        public static RomanNamingResult NormalizeOrGenerateIdentity(
            Gender gender,
            SocialClass socialClass,
            string family,
            RomanName template,
            string branchId,
            List<string> corrections = null,
            Random randomOverride = null)
        {
            return ActiveService.NormalizeIdentity(gender, socialClass, family, template, branchId, corrections, randomOverride);
        }

        public static RomanName GenerateRomanName(Gender gender, string gens = null, SocialClass socialClass = SocialClass.Patrician)
        {
            return GenerateIdentity(gender, socialClass, gens).Name;
        }

        public static RomanName GenerateStandaloneName(
            Gender gender,
            SocialClass socialClass,
            string gens = null,
            Random randomOverride = null)
        {
            return GenerateIdentity(gender, socialClass, gens, null, randomOverride).Name;
        }

        public static RomanName GenerateChildName(
            Character father,
            Character mother,
            Gender gender,
            SocialClass socialClass,
            Random randomOverride = null)
        {
            return GenerateChildIdentity(father, mother, gender, socialClass, randomOverride).Name;
        }

        public static RomanName NormalizeOrGenerateName(
            Gender gender,
            SocialClass socialClass,
            string family,
            RomanName template,
            List<string> corrections = null,
            Random randomOverride = null)
        {
            return NormalizeOrGenerateIdentity(gender, socialClass, family, template, null, corrections, randomOverride).Name;
        }

        public static RomanName NormalizeOrGenerateName(
            Gender gender,
            SocialClass socialClass,
            string family,
            RomanName template,
            string branchId,
            List<string> corrections,
            Random randomOverride)
        {
            return NormalizeOrGenerateIdentity(gender, socialClass, family, template, branchId, corrections, randomOverride).Name;
        }

        public static string ResolveFamilyName(string family, RomanNamingResult result)
        {
            return ActiveService.ResolveFamilyName(family, result);
        }

        public static string ResolveFamilyName(string family, RomanName name)
        {
            return ActiveService.ResolveFamilyName(family, new RomanNamingResult(name, null, false));
        }

        public static string GetFeminineForm(string gens) => RomanNameUtility.ToFeminine(gens);

        public static string GetMasculineForm(string gens) => RomanNameUtility.ToMasculine(gens);

        public static IReadOnlyList<RomanFamilyBranch> GetAllBranches() => ActiveService.GetAllBranches();
    }
}
