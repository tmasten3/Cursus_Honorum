using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Characters;

namespace Game.Systems.Politics.Offices
{
    public class OfficeEligibilityService
    {
        private readonly OfficeState state;

        public OfficeEligibilityService(OfficeState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public bool IsEligible(Character character, OfficeDefinition definition, int year, out string reason)
        {
            reason = null;

            if (character == null)
            {
                reason = "No character";
                return false;
            }

            if (definition == null)
            {
                reason = "No office";
                return false;
            }

            if (!character.IsAlive)
            {
                reason = "Deceased";
                return false;
            }

            if (!character.IsMale)
            {
                reason = "Office restricted to men";
                return false;
            }

            if (character.Age < definition.MinAge)
            {
                reason = "Too young";
                return false;
            }

            if (definition.RequiresPlebeian && character.Class != SocialClass.Plebeian)
            {
                reason = "Requires plebeian status";
                return false;
            }

            if (definition.RequiresPatrician && character.Class != SocialClass.Patrician)
            {
                reason = "Requires patrician status";
                return false;
            }

            if (definition.PrerequisitesAll != null)
            {
                foreach (var prereq in definition.PrerequisitesAll)
                {
                    if (string.IsNullOrWhiteSpace(prereq))
                        continue;

                    if (!HasQualifiedForOffice(character.ID, prereq, year))
                    {
                        reason = $"Requires prior service as {prereq}";
                        return false;
                    }
                }
            }

            if (definition.PrerequisitesAny != null && definition.PrerequisitesAny.Count > 0)
            {
                bool satisfied = definition.PrerequisitesAny.Any(p => HasQualifiedForOffice(character.ID, p, year));
                if (!satisfied)
                {
                    reason = "Prerequisite offices not held";
                    return false;
                }
            }

            if (state.TryGetLastHeldYear(character.ID, definition.Id, out var lastYear))
            {
                int diff = year - lastYear;
                if (diff < definition.ReelectionGapYears)
                {
                    reason = $"Must wait {definition.ReelectionGapYears - diff} more years";
                    return false;
                }
            }

            foreach (var active in state.GetActiveRecords(character.ID))
            {
                if (active.OfficeId == definition.Id && active.EndYear >= year)
                {
                    reason = "Currently holds this office";
                    return false;
                }

                if (active.EndYear > year)
                {
                    reason = "Currently serving another magistracy";
                    return false;
                }
            }

            foreach (var pending in state.GetPendingRecords(character.ID))
            {
                if (pending.OfficeId == definition.Id)
                {
                    reason = "Already elected to this office";
                    return false;
                }

                if (pending.StartYear >= year)
                {
                    reason = "Awaiting another magistracy";
                    return false;
                }
            }

            return true;
        }

        public IReadOnlyList<OfficeDefinition> GetEligibleOffices(Character character, IEnumerable<OfficeDefinition> definitions, int year)
        {
            var result = new List<OfficeDefinition>();
            if (definitions == null)
                return result;

            foreach (var def in definitions)
            {
                if (IsEligible(character, def, year, out _))
                    result.Add(def);
            }

            return result.OrderBy(d => d.Rank).ThenBy(d => d.MinAge).ToList();
        }

        private bool HasQualifiedForOffice(int characterId, string officeId, int electionYear)
        {
            var normalized = OfficeDefinitions.NormalizeOfficeId(officeId);
            if (string.IsNullOrEmpty(normalized))
                return false;

            if (state.HasCompletedOffice(characterId, normalized))
                return true;

            foreach (var active in state.GetActiveRecords(characterId))
            {
                if (active.OfficeId == normalized && active.EndYear <= electionYear)
                    return true;
            }

            return false;
        }
    }
}
