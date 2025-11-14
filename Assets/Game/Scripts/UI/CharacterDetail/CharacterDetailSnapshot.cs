using System;
using Game.Data.Characters;

namespace Game.UI.CharacterDetail
{
    [Serializable]
    public readonly struct CharacterDetailSnapshot
    {
        public CharacterDetailSnapshot(
            int characterId,
            string fullName,
            Gender gender,
            SocialClass socialClass,
            bool isAlive,
            int age,
            int birthYear,
            int birthMonth,
            int birthDay,
            string familyName,
            string identitySummary,
            string familySummary,
            string officesSummary,
            string traitsSummary,
            string electionsSummary)
        {
            CharacterId = characterId;
            FullName = fullName ?? string.Empty;
            Gender = gender;
            SocialClass = socialClass;
            IsAlive = isAlive;
            Age = age;
            BirthYear = birthYear;
            BirthMonth = birthMonth;
            BirthDay = birthDay;
            FamilyName = familyName ?? string.Empty;
            IdentitySummary = identitySummary ?? string.Empty;
            FamilySummary = familySummary ?? string.Empty;
            OfficesSummary = officesSummary ?? string.Empty;
            TraitsSummary = traitsSummary ?? string.Empty;
            ElectionsSummary = electionsSummary ?? string.Empty;
        }

        public int CharacterId { get; }
        public string FullName { get; }
        public Gender Gender { get; }
        public SocialClass SocialClass { get; }
        public bool IsAlive { get; }
        public int Age { get; }
        public int BirthYear { get; }
        public int BirthMonth { get; }
        public int BirthDay { get; }
        public string FamilyName { get; }
        public string IdentitySummary { get; }
        public string FamilySummary { get; }
        public string OfficesSummary { get; }
        public string TraitsSummary { get; }
        public string ElectionsSummary { get; }
    }
}
