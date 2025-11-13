using System;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    public static class SimulationConfigLoader
    {
        public const string DefaultPath = "Assets/Game/Data/simulation_config.json";

        public static SimulationConfig LoadOrDefault(string path = DefaultPath)
        {
            var defaults = new SimulationConfig();
            if (string.IsNullOrWhiteSpace(path))
            {
                Logger.Warn("Config", "Simulation config path was empty. Using defaults.");
                return defaults;
            }

            try
            {
                if (!File.Exists(path))
                {
                    Logger.Warn("Config", $"Simulation config not found at '{path}'. Using defaults.");
                    return defaults;
                }

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Logger.Warn("Config", $"Simulation config at '{path}' was empty. Using defaults.");
                    return defaults;
                }

                var loaded = new SimulationConfig();
                JsonUtility.FromJsonOverwrite(json, loaded);

                return Normalize(loaded, defaults);
            }
            catch (Exception ex)
            {
                Logger.Error("Config", $"Failed to load simulation config from '{path}': {ex.Message}");
                return defaults;
            }
        }

        private static SimulationConfig Normalize(SimulationConfig config, SimulationConfig defaults)
        {
            config ??= new SimulationConfig();

            config.Character ??= new SimulationConfig.CharacterSettings();
            if (string.IsNullOrWhiteSpace(config.Character.BaseDataPath))
                config.Character.BaseDataPath = defaults.Character.BaseDataPath;
            config.Character.Mortality ??= new SimulationConfig.MortalitySettings();
            config.Character.Mortality.AgeBands ??= Array.Empty<SimulationConfig.MortalityBand>();
            if (config.Character.Mortality.AgeBands.Length == 0)
                config.Character.Mortality.AgeBands = defaults.Character.Mortality.AgeBands;

            config.Birth ??= new SimulationConfig.BirthSettings();
            config.Marriage ??= new SimulationConfig.MarriageSettings();

            return config;
        }
    }
}
