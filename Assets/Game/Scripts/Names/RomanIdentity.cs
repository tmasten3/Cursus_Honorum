namespace Game.Data.Characters
{
    /// <summary>
    /// Bundles a generated Roman name with its resolved gens/branch metadata.
    /// </summary>
    public readonly struct RomanIdentity
    {
        public RomanName Name { get; }
        public RomanFamilyBranch Branch { get; }
        public bool CreatedNewBranch { get; }
        public string ParentBranchId { get; }

        public RomanIdentity(RomanName name, RomanFamilyBranch branch, bool createdNewBranch, string parentBranchId)
        {
            Name = name;
            Branch = branch;
            CreatedNewBranch = createdNewBranch;
            ParentBranchId = parentBranchId;
        }

        public bool HasBranch => Branch != null;
    }
}
