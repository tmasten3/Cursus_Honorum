using System;
using System.Collections.Generic;

namespace Game.Core
{
    public interface IGameSystem
    {
        string Name { get; }
        IEnumerable<Type> Dependencies { get; }

        void Initialize(GameState state);
        void Update(GameState state);

        void Shutdown();

        Dictionary<string, object> Save();
        void Load(Dictionary<string, object> data);
    }
}
