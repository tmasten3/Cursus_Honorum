using Game.Data.Characters;

namespace Game.Data.Characters.Generation
{
    internal sealed class BasePopulationGeneratorResult
    {
        public BasePopulationGeneratorResult(CharacterDataWrapper data, PopulationIndex index)
        {
            Data = data;
            Index = index;
        }

        public CharacterDataWrapper Data { get; }
        public PopulationIndex Index { get; }
        public int CharacterCount => Data?.Characters?.Count ?? 0;
    }
}
