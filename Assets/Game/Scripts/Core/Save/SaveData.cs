using System;
using System.Collections.Generic;

namespace Game.Core.Save
{
    /// <summary>
    /// Serializable data transfer object that represents a snapshot of the simulation state.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>
        /// Version of the serialized payload. Used for migrations and compatibility checks.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// UTC timestamp representing when the snapshot was captured.
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Aggregated system state keyed by fully-qualified system type names.
        /// </summary>
        public Dictionary<string, object> Systems { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>
        /// Optional metadata bag for future extensibility (autosave flags, notes, etc.).
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
    }
}
