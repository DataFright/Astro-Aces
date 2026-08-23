using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UTI
{
    /// <summary>
    /// Writes BeanSamples as JSON Lines (one JSON object per sample, not a single top-level
    /// array) - a JSON array can't be safely appended to mid-run the way CSV already streams
    /// row-by-row with periodic flushing, so one-object-per-line sidesteps that entirely. The
    /// real motivation over CSV: extras becomes a real nested object with natively-typed values
    /// instead of one flat "key=value;key=value" string column that needs a second parse pass.
    /// </summary>
    public sealed class JsonlBeanOutput : BeanFileOutputBase
    {
        public JsonlBeanOutput(string filePath, bool append = false) : base(filePath, append) { }

        protected override string FormatLine(BeanSample sample) => BuildLine(sample);

        /// <summary>
        /// Builds one sample's JSON Line. Pure and testable independent of any real file I/O,
        /// same testable/untestable split as CsvBeanOutput.
        /// </summary>
        public static string BuildLine(BeanSample sample)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"tick\":").Append(F(sample.TickIndex)).Append(',');
            sb.Append("\"timestamp\":").Append(F(sample.Timestamp)).Append(',');
            sb.Append("\"position\":{\"x\":").Append(F(sample.Position.x))
                .Append(",\"y\":").Append(F(sample.Position.y))
                .Append(",\"z\":").Append(F(sample.Position.z)).Append("},");
            sb.Append("\"rotation\":{\"x\":").Append(F(sample.Rotation.x))
                .Append(",\"y\":").Append(F(sample.Rotation.y))
                .Append(",\"z\":").Append(F(sample.Rotation.z))
                .Append(",\"w\":").Append(F(sample.Rotation.w)).Append("},");
            sb.Append("\"extras\":").Append(FormatExtras(sample.Extras));
            sb.Append('}');
            return sb.ToString();
        }

        private static string FormatExtras(Dictionary<string, float> extras)
        {
            if (extras == null)
                return "null";

            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, float> kvp in extras)
            {
                if (!first)
                    sb.Append(',');
                first = false;
                sb.Append('"').Append(EscapeString(kvp.Key)).Append("\":").Append(F(kvp.Value));
            }
            sb.Append('}');
            return sb.ToString();
        }

        // Minimal JSON string escaping - extras keys are developer-supplied strings, not
        // arbitrary untrusted input, but a stray quote/backslash shouldn't produce invalid JSON.
        private static string EscapeString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string F(float value) => value.ToString(CultureInfo.InvariantCulture);
        private static string F(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
