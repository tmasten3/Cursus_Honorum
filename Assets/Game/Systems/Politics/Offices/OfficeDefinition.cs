using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Systems.Politics.Offices
{
    [Serializable]
    public class OfficeDefinition
    {
        public string Id;
        public string Name;
        public OfficeAssembly Assembly;
        public int MinAge;
        public int TermLengthYears = 1;
        public int Seats = 1;
        public int ReelectionGapYears = 10;
        public int Rank = 0;
        public bool RequiresPlebeian;
        public bool RequiresPatrician;
        public List<string> PrerequisitesAll = new();
        public List<string> PrerequisitesAny = new();

        public override string ToString() => $"{Name} ({Id})";
    }

    [Serializable]
    public class OfficeDefinitionCollection
    {
        public List<OfficeDefinition> Offices = new();
    }

    public enum OfficeAssembly
    {
        ComitiaCenturiata,
        ComitiaTributa,
        ConciliumPlebis
    }
}
