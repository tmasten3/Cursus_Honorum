using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.Systems.Politics.Offices
{
    public interface IMagistrateOfficeRepository
    {
        OfficeDefinitionCollection Load();
    }

    public sealed class JsonMagistrateOfficeRepository : IMagistrateOfficeRepository
    {
        private readonly string dataPath;
        private readonly Action<string> logWarn;
        private readonly Action<string> logError;

        public JsonMagistrateOfficeRepository(string dataPath, Action<string> logWarn = null, Action<string> logError = null)
        {
            if (string.IsNullOrWhiteSpace(dataPath))
                throw new ArgumentException("Data path must be provided for the magistrate office repository.", nameof(dataPath));

            this.dataPath = dataPath;
            this.logWarn = logWarn;
            this.logError = logError;
        }

        public OfficeDefinitionCollection Load()
        {
            if (!File.Exists(dataPath))
            {
                logWarn?.Invoke($"Magistrate office data file not found at '{dataPath}'.");
                return new OfficeDefinitionCollection();
            }

            try
            {
                var json = File.ReadAllText(dataPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    logWarn?.Invoke($"Magistrate office data file '{dataPath}' was empty.");
                    return new OfficeDefinitionCollection();
                }

                var rawCollection = JsonUtility.FromJson<SerializableOfficeDefinitionCollection>(json);
                if (rawCollection?.Offices == null)
                {
                    logWarn?.Invoke($"Magistrate office data file '{dataPath}' did not contain any offices.");
                    return new OfficeDefinitionCollection();
                }

                return MapDefinitions(rawCollection);
            }
            catch (Exception ex)
            {
                logError?.Invoke($"Failed to load magistrate office data from '{dataPath}': {ex.Message}");
                return new OfficeDefinitionCollection();
            }
        }

        private OfficeDefinitionCollection MapDefinitions(SerializableOfficeDefinitionCollection rawCollection)
        {
            var mapped = new OfficeDefinitionCollection();
            foreach (var raw in rawCollection.Offices)
            {
                if (raw == null)
                    continue;

                var definition = new OfficeDefinition
                {
                    Id = raw.Id,
                    Name = raw.Name,
                    Assembly = ParseAssembly(raw.Assembly),
                    MinAge = raw.MinAge,
                    TermLengthYears = raw.TermLengthYears,
                    Seats = raw.Seats,
                    ReelectionGapYears = raw.ReelectionGapYears,
                    Rank = raw.Rank,
                    RequiresPlebeian = raw.RequiresPlebeian,
                    RequiresPatrician = raw.RequiresPatrician,
                    PrerequisitesAll = raw.PrerequisitesAll != null ? new List<string>(raw.PrerequisitesAll) : new List<string>(),
                    PrerequisitesAny = raw.PrerequisitesAny != null ? new List<string>(raw.PrerequisitesAny) : new List<string>()
                };

                mapped.Offices.Add(definition);
            }

            return mapped;
        }

        private OfficeAssembly ParseAssembly(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return OfficeAssembly.ComitiaCenturiata;

            if (Enum.TryParse(assemblyName, true, out OfficeAssembly assembly))
                return assembly;

            logWarn?.Invoke($"Unrecognized office assembly '{assemblyName}'. Defaulting to {OfficeAssembly.ComitiaCenturiata}.");
            return OfficeAssembly.ComitiaCenturiata;
        }

        [Serializable]
        private class SerializableOfficeDefinitionCollection
        {
            public List<SerializableOfficeDefinition> Offices = new();
        }

        [Serializable]
        private class SerializableOfficeDefinition
        {
            public string Id;
            public string Name;
            public string Assembly;
            public int MinAge;
            public int TermLengthYears = 1;
            public int Seats = 1;
            public int ReelectionGapYears = 10;
            public int Rank = 0;
            public bool RequiresPlebeian;
            public bool RequiresPatrician;
            public List<string> PrerequisitesAll = new();
            public List<string> PrerequisitesAny = new();
        }
    }
}
