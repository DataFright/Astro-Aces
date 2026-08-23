using UnityEngine;

namespace UTI
{
    // Meant for low-frequency/dev use - at a high capture rate this will flood the console fast.
    // Not solving that in v1 (e.g. throttling); CSV is the better fit for high-rate capture.
    public sealed class ConsoleBeanOutput : IBeanOutput
    {
        public void Open(BeanTracker tracker) { }

        public void Write(BeanSample sample) => Debug.Log(Format(sample));

        public void Close() { }

        private static string Format(BeanSample sample)
        {
            return $"[Bean] tick={sample.TickIndex} t={sample.Timestamp:F2} pos={sample.Position} rot={sample.Rotation.eulerAngles}";
        }
    }
}
