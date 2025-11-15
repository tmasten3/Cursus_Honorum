using System;
using System.IO;
using UnityEngine;

namespace Game.Systems.Population
{
    [Serializable]
    public class BirthSettings
    {
        public int RngSeed = 1338;
        public int FemaleMinAge = 14;
        public int FemaleMaxAge = 35;
        public float DailyBirthChanceIfMarried = 0.0015f;
        public int GestationDays = 270;
        public float MultipleBirthChance = 0.02f;
    }

    [Serializable]
    public class MarriageSettings
    {
        public int RngSeed = 2025;
        public int MinAgeMale = 14;
        public int MinAgeFemale = 12;
        public int DailyMatchmakingCap = 10;
        public float DailyMarriageChanceWhenEligible = 0.002f;
        public float PreferSameClassWeight = 1.5f;
        public bool CrossClassAllowed = true;
    }

    [Serializable]
    public class PopulationSimulationConfig
    {
        public BirthSettings Birth = new BirthSettings();
        public MarriageSettings Marriage = new MarriageSettings();
    }

    public static class PopulationSimulationConfigLoader
    {
        public const string DefaultConfigPath = "Assets/Game/Data/population_simulation.json";

        public static PopulationSimulationConfig Load(
            string path,
            Action<string> logInfo = null,
            Action<string> logWarn = null,
            Action<string> logError = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                logWarn?.Invoke("Population simulation config path was empty. Using defaults.");
                return new PopulationSimulationConfig();
            }

            if (!File.Exists(path))
            {
                logWarn?.Invoke($"Population simulation config file not found at '{path}'. Using defaults.");
                return new PopulationSimulationConfig();
            }

            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    logWarn?.Invoke($"Population simulation config file '{path}' was empty. Using defaults.");
                    return new PopulationSimulationConfig();
                }

                PopulationSimulationConfig config;
                try
                {
                    config = JsonUtility.FromJson<PopulationSimulationConfig>(json);
                }
                catch (Exception parseEx)
                {
                    logError?.Invoke($"Failed to parse population simulation config '{path}': {parseEx.Message}");
                    return new PopulationSimulationConfig();
                }

                if (config == null)
                {
                    logWarn?.Invoke($"Population simulation config '{path}' was null after parsing. Using defaults.");
                    return new PopulationSimulationConfig();
                }

                if (config.Birth == null)
                    config.Birth = new BirthSettings();
                if (config.Marriage == null)
                    config.Marriage = new MarriageSettings();

                logInfo?.Invoke($"Loaded population simulation config from '{path}'.");
                return config;
            }
            catch (Exception ex)
            {
                logError?.Invoke($"Failed to load population simulation config '{path}': {ex.Message}");
                return new PopulationSimulationConfig();
            }
        }
    }
}
