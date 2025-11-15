using System;

namespace Game.Data.Characters
{
    /// <summary>
    /// Represents the result of Roman naming resolution, including branch metadata.
    /// </summary>
    internal readonly struct RomanIdentity
    {
        public RomanIdentity(
            RomanName name,
            RomanGensVariant variant,
            string branchId,
            SocialClass socialClass)
        {
            Name = name;
            Variant = variant;
            BranchId = branchId;
            SocialClass = socialClass;
        }

        public RomanName Name { get; }

        public RomanGensVariant Variant { get; }

        public RomanGensDefinition Definition => Variant?.Definition;

        public SocialClass SocialClass { get; }

        public string BranchId { get; }

        public string Family => Definition?.StylizedNomen;

        public string FeminineFamily => Definition?.FeminineNomen;

        public string Cognomen => Name?.Cognomen;

        public bool IsValid => Name != null && Variant != null && !string.IsNullOrEmpty(Family);

        public RomanIdentity WithName(RomanName updated)
        {
            if (updated == null)
                throw new ArgumentNullException(nameof(updated));

            return new RomanIdentity(updated, Variant, BranchId, SocialClass);
        }
    }
}
