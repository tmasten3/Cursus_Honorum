using Game.Core;

namespace Game.Systems.EventBus
{
    /// <summary>
    /// Fired when a new character is born and added to the world.
    /// Typically published by BirthSystem.
    /// </summary>
    public class OnCharacterBorn : GameEvent
    {
        public int ChildID { get; }
        public int? FatherID { get; }
        public int MotherID { get; }

        public OnCharacterBorn(int year, int month, int day, int childID, int? fatherID, int motherID)
            : base(nameof(OnCharacterBorn), year, month, day)
        {
            ChildID = childID;
            FatherID = fatherID;
            MotherID = motherID;
        }
    }

    /// <summary>
    /// Fired when a character dies (for any cause).
    /// Published by CharacterSystem.
    /// </summary>
    public class OnCharacterDied : GameEvent
    {
        public int CharacterID { get; }
        public string Cause { get; }

        public OnCharacterDied(int year, int month, int day, int characterID, string cause)
            : base(nameof(OnCharacterDied), year, month, day)
        {
            CharacterID = characterID;
            Cause = cause;
        }
    }

    /// <summary>
    /// Fired when two characters are successfully married.
    /// Published by MarriageSystem.
    /// </summary>
    public class OnCharacterMarried : GameEvent
    {
        public int SpouseA { get; }
        public int SpouseB { get; }

        public OnCharacterMarried(int year, int month, int day, int spouseA, int spouseB)
            : base(nameof(OnCharacterMarried), year, month, day)
        {
            SpouseA = spouseA;
            SpouseB = spouseB;
        }
    }

    /// <summary>
    /// Fired daily to summarize population statistics.
    /// Published by CharacterSystem after mortality updates.
    /// </summary>
    public class OnPopulationTick : GameEvent
    {
        public int Births { get; }
        public int Deaths { get; }
        public int Marriages { get; }

        public OnPopulationTick(int year, int month, int day, int births, int deaths, int marriages)
            : base(nameof(OnPopulationTick), year, month, day)
        {
            Births = births;
            Deaths = deaths;
            Marriages = marriages;
        }
    }

    /// <summary>
    /// Fired when a character's ambition focus or intensity changes.
    /// Published by CharacterSystem during annual lifecycle updates.
    /// </summary>
    public class OnCharacterAmbitionChanged : GameEvent
    {
        public int CharacterID { get; }
        public string PreviousGoal { get; }
        public string CurrentGoal { get; }
        public int PreviousIntensity { get; }
        public int CurrentIntensity { get; }
        public int? PreviousTargetYear { get; }
        public int? CurrentTargetYear { get; }
        public bool IsRetired { get; }

        public OnCharacterAmbitionChanged(int year, int month, int day, int characterId, string previousGoal, string currentGoal,
            int previousIntensity, int currentIntensity, int? previousTargetYear, int? currentTargetYear, bool isRetired)
            : base(nameof(OnCharacterAmbitionChanged), year, month, day)
        {
            CharacterID = characterId;
            PreviousGoal = previousGoal;
            CurrentGoal = currentGoal;
            PreviousIntensity = previousIntensity;
            CurrentIntensity = currentIntensity;
            PreviousTargetYear = previousTargetYear;
            CurrentTargetYear = currentTargetYear;
            IsRetired = isRetired;
        }
    }

    /// <summary>
    /// Fired when a character retires from active ambition.
    /// Published by CharacterSystem.
    /// </summary>
    public class OnCharacterRetired : GameEvent
    {
        public int CharacterID { get; }
        public string PreviousGoal { get; }
        public string Notes { get; }

        public OnCharacterRetired(int year, int month, int day, int characterId, string previousGoal, string notes)
            : base(nameof(OnCharacterRetired), year, month, day)
        {
            CharacterID = characterId;
            PreviousGoal = previousGoal;
            Notes = notes;
        }
    }

    /// <summary>
    /// Fired when a character levels up a trait.
    /// Published by CharacterSystem when trait progress thresholds are met.
    /// </summary>
    public class OnCharacterTraitAdvanced : GameEvent
    {
        public int CharacterID { get; }
        public string TraitId { get; }
        public int PreviousLevel { get; }
        public int NewLevel { get; }

        public OnCharacterTraitAdvanced(int year, int month, int day, int characterId, string traitId, int previousLevel, int newLevel)
            : base(nameof(OnCharacterTraitAdvanced), year, month, day)
        {
            CharacterID = characterId;
            TraitId = traitId;
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
        }
    }

    /// <summary>
    /// Fired whenever a notable career milestone is recorded for a character.
    /// Published by CharacterSystem.
    /// </summary>
    public class OnCharacterCareerMilestoneRecorded : GameEvent
    {
        public int CharacterID { get; }
        public string Title { get; }
        public string Notes { get; }

        public OnCharacterCareerMilestoneRecorded(int year, int month, int day, int characterId, string title, string notes)
            : base(nameof(OnCharacterCareerMilestoneRecorded), year, month, day)
        {
            CharacterID = characterId;
            Title = title;
            Notes = notes;
        }
    }
}
