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
}
