using System;
using System.Collections.Generic;
using Game.Systems.EventBus;

namespace Game.Core.Save
{
    public sealed class OnGameSavedEvent : GameEvent
    {
        public OnGameSavedEvent(
            string slotName,
            string filePath,
            DateTime fileTimestampUtc,
            long sizeInBytes,
            int version,
            DateTime snapshotTimestampUtc,
            IReadOnlyList<string> validationWarnings,
            int year,
            int month,
            int day)
            : base(nameof(OnGameSavedEvent), EventCategory.Debug, year, month, day)
        {
            SlotName = slotName ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            FileTimestampUtc = fileTimestampUtc;
            SnapshotTimestampUtc = snapshotTimestampUtc;
            SizeInBytes = Math.Max(0, sizeInBytes);
            Version = version;
            ValidationWarnings = validationWarnings ?? Array.Empty<string>();
        }

        public string SlotName { get; }
        public string FilePath { get; }
        public DateTime FileTimestampUtc { get; }
        public DateTime SnapshotTimestampUtc { get; }
        public long SizeInBytes { get; }
        public int Version { get; }
        public IReadOnlyList<string> ValidationWarnings { get; }
    }

    public sealed class OnGameLoadedEvent : GameEvent
    {
        public OnGameLoadedEvent(
            string slotName,
            string filePath,
            DateTime fileTimestampUtc,
            DateTime loadedAtUtc,
            int version,
            DateTime snapshotTimestampUtc,
            IReadOnlyList<string> validationWarnings,
            int year,
            int month,
            int day)
            : base(nameof(OnGameLoadedEvent), EventCategory.Debug, year, month, day)
        {
            SlotName = slotName ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            FileTimestampUtc = fileTimestampUtc;
            LoadedAtUtc = loadedAtUtc;
            SnapshotTimestampUtc = snapshotTimestampUtc;
            Version = version;
            ValidationWarnings = validationWarnings ?? Array.Empty<string>();
        }

        public string SlotName { get; }
        public string FilePath { get; }
        public DateTime FileTimestampUtc { get; }
        public DateTime LoadedAtUtc { get; }
        public DateTime SnapshotTimestampUtc { get; }
        public int Version { get; }
        public IReadOnlyList<string> ValidationWarnings { get; }
    }
}
