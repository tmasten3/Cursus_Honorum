using System;
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

                var collection = JsonUtility.FromJson<OfficeDefinitionCollection>(json);
                if (collection?.Offices == null)
                {
                    logWarn?.Invoke($"Magistrate office data file '{dataPath}' did not contain any offices.");
                    return new OfficeDefinitionCollection();
                }

                return collection;
            }
            catch (Exception ex)
            {
                logError?.Invoke($"Failed to load magistrate office data from '{dataPath}': {ex.Message}");
                return new OfficeDefinitionCollection();
            }
        }
    }
}
