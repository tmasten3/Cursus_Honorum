namespace Game.Core
{
    public static class SystemBootstrapProfileExtensions
    {
        public static SystemBootstrapProfile CreateDefaultProfile()
        {
            var profile = new SystemBootstrapProfile();
            profile.LoadDefaultSystems();
            return profile;
        }
    }
}
