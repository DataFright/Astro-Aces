using System.Collections.Generic;
using UnityEngine;

namespace UTI
{
    /// <summary>One captured tick of a Bean's state.</summary>
    public readonly struct BeanSample
    {
        public readonly int TickIndex;
        public readonly float Timestamp;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        // Null unless a custom capture delegate is assigned on the BeanTracker - keeps the
        // common case (no custom fields) from allocating a Dictionary every tick.
        public readonly Dictionary<string, float> Extras;

        public BeanSample(int tickIndex, float timestamp, Vector3 position, Quaternion rotation, Dictionary<string, float> extras = null)
        {
            TickIndex = tickIndex;
            Timestamp = timestamp;
            Position = position;
            Rotation = rotation;
            Extras = extras;
        }
    }
}
