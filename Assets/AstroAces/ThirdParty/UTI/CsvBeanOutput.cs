using System.Collections.Generic;
using System.Globalization;

namespace UTI
{
    /// <summary>
    /// Writes BeanSamples to a CSV file at a fixed path. Buffered writes with a periodic flush -
    /// flushing every row would be a perf trap at high capture rates - and always flushed on
    /// Close(). Extras (if any) are packed into a single trailing "key=value;key=value" column
    /// rather than dynamic per-key columns, since which keys appear can vary sample to sample.
    /// </summary>
    public sealed class CsvBeanOutput : BeanFileOutputBase
    {
        private const string Header = "tick,timestamp,x,y,z,qx,qy,qz,qw,extras";

        public CsvBeanOutput(string filePath, bool append = false) : base(filePath, append) { }

        // Only write the header on a genuinely new file - re-opening the same path in append
        // mode (a pooled object's second+ reuse cycle) should just resume writing rows, not
        // insert a second header row partway through the file.
        protected override string HeaderLine => Header;

        protected override string FormatLine(BeanSample sample) => FormatRow(sample);

        private static string FormatRow(BeanSample sample)
        {
            return string.Join(",",
                F(sample.TickIndex),
                F(sample.Timestamp),
                F(sample.Position.x), F(sample.Position.y), F(sample.Position.z),
                F(sample.Rotation.x), F(sample.Rotation.y), F(sample.Rotation.z), F(sample.Rotation.w),
                FormatExtras(sample.Extras));
        }

        private static string FormatExtras(Dictionary<string, float> extras)
        {
            if (extras == null || extras.Count == 0)
                return string.Empty;

            var parts = new List<string>(extras.Count);
            foreach (KeyValuePair<string, float> kvp in extras)
                parts.Add($"{kvp.Key}={F(kvp.Value)}");

            return string.Join(";", parts);
        }

        private static string F(float value) => value.ToString(CultureInfo.InvariantCulture);
        private static string F(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
