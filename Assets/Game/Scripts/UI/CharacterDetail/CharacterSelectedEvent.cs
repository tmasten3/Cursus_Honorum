using Game.Systems.EventBus;

namespace Game.UI.CharacterDetail
{
    /// <summary>
    /// Lightweight payload describing the currently selected character.
    /// </summary>
    public struct CharacterSelectedEvent
    {
        public int CharacterId;
    }

    internal sealed class CharacterSelectedEventEnvelope : GameEvent
    {
        public CharacterSelectedEventEnvelope(int year, int month, int day, CharacterSelectedEvent payload)
            : base(nameof(CharacterSelectedEvent), year, month, day)
        {
            Payload = payload;
        }

        public CharacterSelectedEvent Payload { get; }
    }
}
