using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Core;
using Game.Systems.Politics.Offices;

namespace Game.Systems.Politics
{
    internal sealed class PoliticsTermTracker
    {
        private readonly Dictionary<int, List<OfficeTermRecord>> historyByCharacter = new();
        private readonly HashSet<(int characterId, string officeId, int startYear, int endYear)> seenRecords = new();

        public void Reset()
        {
            historyByCharacter.Clear();
            seenRecords.Clear();
        }

        public void RecordAssignment(OfficeAssignedEvent assignment)
        {
            if (assignment == null)
                return;

            var officeId = Normalize(assignment.OfficeId);
            var key = (assignment.CharacterId, officeId, assignment.TermStartYear, assignment.TermEndYear);
            if (!seenRecords.Add(key))
                return;

            var record = new OfficeTermRecord(
                officeId,
                assignment.OfficeName ?? officeId,
                assignment.SeatIndex,
                assignment.TermStartYear,
                assignment.TermEndYear);

            InsertSorted(historyByCharacter, assignment.CharacterId, record);
        }

        public void SeedFromHistory(int characterId, IReadOnlyList<OfficeCareerRecord> history,
            Func<string, string> officeNameResolver)
        {
            if (history == null)
                return;

            foreach (var record in history)
            {
                if (record == null)
                    continue;

                var officeId = Normalize(record.OfficeId);
                var key = (characterId, officeId, record.StartYear, record.EndYear);
                if (!seenRecords.Add(key))
                    continue;

                string officeName = officeNameResolver?.Invoke(officeId) ?? officeId;
                var termRecord = new OfficeTermRecord(officeId, officeName, record.SeatIndex, record.StartYear, record.EndYear);
                InsertSorted(historyByCharacter, characterId, termRecord);
            }
        }

        public IReadOnlyList<OfficeTermRecord> GetHistory(int characterId)
        {
            if (!historyByCharacter.TryGetValue(characterId, out var list) || list.Count == 0)
                return Array.Empty<OfficeTermRecord>();

            return new ReadOnlyCollection<OfficeTermRecord>(list);
        }

        private static void InsertSorted(Dictionary<int, List<OfficeTermRecord>> map, int characterId, OfficeTermRecord record)
        {
            if (!map.TryGetValue(characterId, out var list))
            {
                list = new List<OfficeTermRecord>();
                map[characterId] = list;
            }

            int index = list.FindIndex(existing =>
                existing.StartYear > record.StartYear ||
                (existing.StartYear == record.StartYear && string.CompareOrdinal(existing.OfficeId, record.OfficeId) > 0));

            if (index >= 0)
                list.Insert(index, record);
            else
                list.Add(record);
        }

        private static string Normalize(string officeId)
        {
            return OfficeDefinitions.NormalizeOfficeId(officeId) ?? officeId ?? string.Empty;
        }
    }
}
