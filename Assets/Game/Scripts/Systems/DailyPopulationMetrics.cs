using Game.Systems.EventBus;

namespace Game.Systems.CharacterSystem
{
    internal sealed class DailyPopulationMetrics
    {
        public int Births { get; private set; }
        public int Deaths { get; private set; }
        public int Marriages { get; private set; }

        public void Reset()
        {
            Births = 0;
            Deaths = 0;
            Marriages = 0;
        }

        public void RecordBirth() => Births++;
        public void RecordDeath() => Deaths++;
        public void RecordMarriage() => Marriages++;

        public OnPopulationTick ToEvent(int year, int month, int day) =>
            new OnPopulationTick(year, month, day, Births, Deaths, Marriages);
    }
}
