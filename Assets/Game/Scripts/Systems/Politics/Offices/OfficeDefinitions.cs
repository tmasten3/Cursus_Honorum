using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public class OfficeDefinitions
    {
        private readonly Dictionary<string, OfficeDefinition> definitions = new();
        private readonly string dataPath;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarn;
        private readonly Action<string> logError;

        public OfficeDefinitions(string dataPath, Action<string> logInfo, Action<string> logWarn, Action<string> logError)
        {
            this.dataPath = dataPath;
            this.logInfo = logInfo;
            this.logWarn = logWarn;
            this.logError = logError;
        }

        public static string NormalizeOfficeId(string officeId)
        {
            if (string.IsNullOrWhiteSpace(officeId))
                return null;

            return officeId.Trim().ToLowerInvariant();
        }

        public void LoadDefinitions()
        {
            definitions.Clear();

            if (!File.Exists(dataPath))
            {
                logWarn?.Invoke($"No office definition file found at {dataPath}.");
                return;
            }

            try
            {
                var json = File.ReadAllText(dataPath);
                OfficeDefinitionCollection wrapper;
                try
                {
                    wrapper = JsonUtility.FromJson<OfficeDefinitionCollection>(json);
                }
                catch (Exception parseEx)
                {
                    logError?.Invoke($"Failed to parse office definition file '{dataPath}': {parseEx.Message}");
                    return;
                }

                if (wrapper?.Offices == null)
                {
                    logWarn?.Invoke("Office definition file did not contain any offices.");
                    return;
                }

                for (int i = 0; i < wrapper.Offices.Count; i++)
                {
                    var def = wrapper.Offices[i];
                    if (def == null)
                    {
                        Game.Core.Logger.Warn("Safety", $"{dataPath}: Office entry at index {i} was null. Skipping.");
                        continue;
                    }

                    var normalizedId = NormalizeOfficeId(def.Id);
                    if (normalizedId == null)
                    {
                        Game.Core.Logger.Warn("Safety", $"{dataPath}: Office entry at index {i} missing identifier.");
                        continue;
                    }

                    def.Id = normalizedId;
                    def.Name ??= def.Id;
                    def.Seats = Math.Max(1, def.Seats);
                    def.TermLengthYears = Math.Max(1, def.TermLengthYears);
                    def.ReelectionGapYears = Math.Max(0, def.ReelectionGapYears);

                    definitions[def.Id] = def;
                }

                logInfo?.Invoke($"Loaded {definitions.Count} offices from data file.");
            }
            catch (Exception ex)
            {
                logError?.Invoke($"Failed to load office definitions: {ex.Message}");
            }
        }

        public OfficeDefinition GetDefinition(string officeId)
        {
            var normalized = NormalizeOfficeId(officeId);
            if (normalized != null && definitions.TryGetValue(normalized, out var def))
                return def;
            return null;
        }

        public IReadOnlyList<OfficeDefinition> GetAllDefinitions() => definitions.Values.ToList();

        public int Count => definitions.Count;
    }
}
