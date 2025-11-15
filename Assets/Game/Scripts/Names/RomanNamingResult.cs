namespace Game.Data.Characters
{
    /// <summary>
    /// Result of a naming operation, including the generated name and branch metadata.
    /// </summary>
    public sealed class RomanNamingResult
    {
        public RomanNamingResult(RomanName name, RomanFamilyBranch branch, bool branchCreated)
        {
            Name = name;
            Branch = branch;
            BranchCreated = branchCreated;
        }

        public RomanName Name { get; }
        public RomanFamilyBranch Branch { get; }
        public bool BranchCreated { get; }

        public string BranchId => Branch?.Id;
    }
}
