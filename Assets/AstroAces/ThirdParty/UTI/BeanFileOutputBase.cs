using System.IO;
using System.Text;

namespace UTI
{
    // CsvBeanOutput and JsonlBeanOutput are both "buffered rows to a text file" outputs that
    // differ only in how one BeanSample becomes one line and whether a header exists - extracted
    // here once both existed and the StreamWriter-lifecycle code between them was identical
    // (directory creation, flush-interval batching, Close()'s flush+close+null). Public only
    // because CsvBeanOutput/JsonlBeanOutput are public and C# requires a base class to be at
    // least as accessible as its subclass (CS0060) - not meant as part of UTI's own
    // extensibility surface, which is still IBeanOutput (see BeanLogger.CustomOutputs).
    public abstract class BeanFileOutputBase : IBeanOutput
    {
        private const int FlushInterval = 32;

        protected readonly string FilePath;
        protected readonly bool Append;

        private StreamWriter writer;
        private int rowsSinceFlush;

        protected BeanFileOutputBase(string filePath, bool append)
        {
            FilePath = filePath;
            Append = append;
        }

        // A line to write once, only on a genuinely new file (null = no header, e.g. JSON Lines).
        // Checked before the StreamWriter opens the file, so append-mode's "does it already exist"
        // question is answered honestly rather than against a file this same call just created.
        protected virtual string HeaderLine => null;

        public virtual void Open(BeanTracker tracker)
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string header = HeaderLine;
            bool writeHeader = header != null && (!Append || !File.Exists(FilePath));

            writer = new StreamWriter(FilePath, Append, Encoding.UTF8);
            if (writeHeader)
                writer.WriteLine(header);

            rowsSinceFlush = 0;
        }

        public void Write(BeanSample sample)
        {
            if (writer == null)
                return;

            writer.WriteLine(FormatLine(sample));

            rowsSinceFlush++;
            if (rowsSinceFlush >= FlushInterval)
            {
                writer.Flush();
                rowsSinceFlush = 0;
            }
        }

        public void Close()
        {
            if (writer == null)
                return;

            writer.Flush();
            writer.Close();
            writer = null;
        }

        protected abstract string FormatLine(BeanSample sample);
    }
}
