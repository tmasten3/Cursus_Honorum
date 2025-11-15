using System;
namespace Game.Data.Characters
{
    /// <summary>
    /// Represents a persistent cognomen branch within a gens/social-class variant.
    /// </summary>
    internal sealed class RomanFamilyBranch
    {
        public string Id { get; }
        public RomanGensVariant Variant { get; }
        public string Cognomen { get; }
        public string ParentBranchId { get; }
        public bool IsDynamic { get; }

        public RomanFamilyBranch(string id, RomanGensVariant variant, string cognomen, bool isDynamic, string parentBranchId)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Variant = variant ?? throw new ArgumentNullException(nameof(variant));
            Cognomen = RomanNameUtility.Normalize(cognomen);
            ParentBranchId = string.IsNullOrWhiteSpace(parentBranchId) ? null : parentBranchId;
            IsDynamic = isDynamic;
        }

        public RomanGensDefinition Definition => Variant.Definition;
        public string StylizedNomen => Definition.StylizedNomen;
        public string FeminineNomen => Definition.FeminineNomen;
        public SocialClass SocialClass => Variant.SocialClass;

        public override string ToString()
        {
            var cognomen = string.IsNullOrEmpty(Cognomen) ? "(no cognomen)" : Cognomen;
            return $"{StylizedNomen} ({SocialClass}) — {cognomen}";
        }
    }
}
