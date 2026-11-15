using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>Null-capable frequency counter using an injected comparer.</summary>
    /// <example><![CDATA[
    /// var counts = new ValueCounter<string>(StringComparer.OrdinalIgnoreCase);
    /// counts.Add("stone");
    /// int count = counts.Count("STONE"); // 1
    /// ]]></example>
    public class ValueCounter<T>
    {
        private readonly Dictionary<Key, int> counts;

        private readonly struct Key
        {
            public readonly T value;
            public Key(T value) { this.value = value; }
        }

        private class KeyComparer : IEqualityComparer<Key>
        {
            private readonly IEqualityComparer<T> comparer;
            public KeyComparer(IEqualityComparer<T> comparer) { this.comparer = comparer; }
            public bool Equals(Key x, Key y) => comparer.Equals(x.value, y.value);
            public int GetHashCode(Key key) => key.value is null ? 0 : comparer.GetHashCode(key.value);
        }

        /// <summary>Creates a counter with the caller's value comparer.</summary>
        public ValueCounter(IEqualityComparer<T> comparer)
        {
            counts = new Dictionary<Key, int>(new KeyComparer(comparer));
        }

        /// <summary>Adds a value count, including for null values.</summary>
        public void Add(T value, int count = 1)
        {
            var key = new Key(value);
            counts.TryGetValue(key, out int existing);
            counts[key] = checked(existing + count);
        }

        /// <summary>Gets the count for a value according to the injected comparer.</summary>
        public int Count(T value) => counts.TryGetValue(new Key(value), out int count) ? count : 0;

        /// <summary>Gets the most frequent value, or fallback when no values were added.</summary>
        public T MostCommon(T fallback)
        {
            T result = fallback;
            int best = -1;
            foreach (var entry in counts)
                if (entry.Value > best)
                {
                    result = entry.Key.value;
                    best = entry.Value;
                }
            return result;
        }

        /// <summary>Copies all distinct values and counts into a list.</summary>
        public List<KeyValuePair<T, int>> ToList()
        {
            var result = new List<KeyValuePair<T, int>>(counts.Count);
            foreach (var entry in counts)
                result.Add(new KeyValuePair<T, int>(entry.Key.value, entry.Value));
            return result;
        }
    }
}
