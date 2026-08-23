using System;
using System.Collections;
using System.Collections.Generic;

namespace UTI
{
    /// <summary>
    /// Fixed-capacity circular buffer of BeanSamples. Once full, adding a new sample
    /// overwrites the oldest. Index 0 is always the oldest sample currently held,
    /// Count-1 the newest - no shifting, no per-add allocation.
    /// </summary>
    public sealed class BeanBuffer : IReadOnlyList<BeanSample>
    {
        private readonly BeanSample[] samples;
        private int head; // slot the next Add() will write to
        private int count;

        public BeanBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Buffer capacity must be greater than zero.");

            samples = new BeanSample[capacity];
        }

        public int Capacity => samples.Length;
        public int Count => count;

        public void Add(BeanSample sample)
        {
            samples[head] = sample;
            head = (head + 1) % samples.Length;
            if (count < samples.Length)
                count++;
        }

        public void Clear()
        {
            head = 0;
            count = 0;
        }

        public BeanSample this[int index]
        {
            get
            {
                if (index < 0 || index >= count)
                    throw new IndexOutOfRangeException();

                int oldest = count < samples.Length ? 0 : head;
                return samples[(oldest + index) % samples.Length];
            }
        }

        public IEnumerator<BeanSample> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
