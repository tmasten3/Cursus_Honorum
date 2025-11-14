using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Characters;
using Game.Systems.Politics.Offices;
using UnityEngine;

namespace Game.Systems.Politics.Elections
{
    public class ElectionVoteSimulator
    {
        private readonly System.Random rng;

        public ElectionVoteSimulator(System.Random rng)
        {
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public void ScoreCandidates(OfficeDefinition office, List<ElectionCandidate> candidates)
        {
            if (office == null || candidates == null)
                return;

            foreach (var candidate in candidates)
            {
                if (candidate == null)
                    continue;

                var (score, breakdown) = EvaluateVote(office, candidate);
                candidate.FinalScore = score;
                foreach (var kv in breakdown)
                {
                    candidate.VoteBreakdown[kv.Key] = kv.Value;
                }
            }
        }

        public List<ElectionCandidate> SelectWinners(List<ElectionCandidate> candidates, int seatCount)
        {
            var pool = new List<ElectionCandidate>(candidates ?? new List<ElectionCandidate>());
            var winners = new List<ElectionCandidate>();
            seatCount = Mathf.Max(1, seatCount);

            for (int seat = 0; seat < seatCount && pool.Count > 0; seat++)
            {
                float total = pool.Sum(c => Mathf.Max(0.1f, c.FinalScore));
                double roll = rng.NextDouble() * total;
                double accum = 0;

                for (int i = 0; i < pool.Count; i++)
                {
                    var candidate = pool[i];
                    accum += Mathf.Max(0.1f, candidate.FinalScore);
                    if (roll <= accum)
                    {
                        winners.Add(candidate);
                        pool.RemoveAt(i);
                        break;
                    }
                }
            }

            return winners;
        }

        private (float score, Dictionary<string, float> breakdown) EvaluateVote(OfficeDefinition office, ElectionCandidate candidate)
        {
            var breakdown = new Dictionary<string, float>();
            var c = candidate.Character;

            float influence = c.Influence * 10f;
            float wealth = Mathf.Sqrt(Mathf.Max(0, c.Wealth)) * 1.2f;
            float charisma = HasTrait(c, "Charismatic") ? 12f : 0f;
            float popularity = HasTrait(c, "Popular") ? 10f : c.Influence * 2f;
            float faction = HasTrait(c, "Populist") ? 8f : 4f;

            switch (office.Assembly)
            {
                case OfficeAssembly.ComitiaCenturiata:
                    breakdown["Dignitas"] = influence;
                    breakdown["Wealth"] = wealth * 1.3f;
                    breakdown["Family"] = c.Class == SocialClass.Patrician ? 14f : 6f;
                    breakdown["Allies"] = faction;
                    break;
                case OfficeAssembly.ComitiaTributa:
                    breakdown["Popularity"] = popularity;
                    breakdown["Charisma"] = charisma > 0 ? charisma : c.Influence * 1.5f;
                    breakdown["Wealth"] = wealth * 0.8f;
                    breakdown["Networks"] = c.Class == SocialClass.Plebeian ? 8f : 5f;
                    break;
                case OfficeAssembly.ConciliumPlebis:
                    breakdown["Popularity"] = popularity * 1.2f;
                    breakdown["Advocacy"] = HasTrait(c, "Populist") ? 14f : 6f;
                    breakdown["Plebeian"] = c.Class == SocialClass.Plebeian ? 12f : 2f;
                    breakdown["Charisma"] = charisma > 0 ? charisma : c.Influence * 1.2f;
                    break;
            }

            float ambition = candidate.Declaration?.DesireScore ?? 0f;
            breakdown["Campaign"] = ambition * 0.25f;

            float fortuna = (float)(rng.NextDouble() * 12f);
            breakdown["Fortuna"] = fortuna;

            float total = breakdown.Values.Sum();
            return (total, breakdown);
        }

        private bool HasTrait(Character character, string trait)
        {
            if (character?.Traits == null)
                return false;

            return character.Traits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase));
        }
    }
}
