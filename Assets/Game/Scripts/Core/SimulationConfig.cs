using System;

namespace Game.Core
{
    [Serializable]
    public sealed class SimulationConfig
    {
        public CharacterSettings Character = new CharacterSettings();
        public BirthSettings Birth = new BirthSettings();
        public MarriageSettings Marriage = new MarriageSettings();

        [Serializable]
        public sealed class CharacterSettings
        {
            public int RngSeed = 1337;
            public bool KeepDeadInMemory = true;
            public string BaseDataPath = "generated://base_population";
            public MortalitySettings Mortality = new MortalitySettings();
        }

        [Serializable]
        public sealed class MortalitySettings
        {
            public bool UseAgeBandHazards = true;
            public MortalityBand[] AgeBands =
            {
                new MortalityBand { Min = 0, Max = 4, YearlyHazard = 0.08f },
                new MortalityBand { Min = 5, Max = 14, YearlyHazard = 0.01f },
                new MortalityBand { Min = 15, Max = 29, YearlyHazard = 0.007f },
                new MortalityBand { Min = 30, Max = 44, YearlyHazard = 0.012f },
                new MortalityBand { Min = 45, Max = 59, YearlyHazard = 0.03f },
                new MortalityBand { Min = 60, Max = 74, YearlyHazard = 0.08f },
                new MortalityBand { Min = 75, Max = 110, YearlyHazard = 0.20f }
            };
        }

        [Serializable]
        public sealed class MortalityBand
        {
            public int Min;
            public int Max;
            public float YearlyHazard;

            [NonSerialized]
            public float DailyHazard;
        }

        [Serializable]
        public sealed class BirthSettings
        {
            public int RngSeed = 1338;
            public int FemaleMinAge = 14;
            public int FemaleMaxAge = 35;
            public float DailyBirthChanceIfMarried = 0.0015f;
            public int GestationDays = 270;
            public float MultipleBirthChance = 0.02f;
        }

        [Serializable]
        public sealed class MarriageSettings
        {
            public int RngSeed = 2025;
            public int MinAgeMale = 14;
            public int MinAgeFemale = 12;
            public int DailyMatchmakingCap = 10;
            public float DailyMarriageChanceWhenEligible = 0.002f;
            public float PreferSameClassWeight = 1.5f;
            public bool CrossClassAllowed = true;
        }
    }
}
