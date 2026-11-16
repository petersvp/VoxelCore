using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>A 2D chunk that stores only cells different from its default value.</summary>
    /// <remarks>Assigning the default removes a stored entry. Enumerate yields stored entries only, even with includeDefault=true. Cell operations may run concurrently; callers must coordinate changes to Size or DefaultValue and optimization or conversion with writes.</remarks>
    /// <example><![CDATA[
    /// var chunk = new SparseChunk2D<byte, int>(16, 0);
    /// chunk[2, 3] = 7;
    /// chunk[2, 3] = 0; // Removes the entry.
    /// ]]></example>
    public class SparseChunk2D<K, T> : IChunk2D<T> where K : unmanaged
    {
        private readonly struct Cell
        {
            public readonly T value;
            public Cell(T value) { this.value = value; }
        }

        private readonly ConcurrentDictionary<(K, K), Cell> tiles = new();
        /// <summary>Comparer used for default values and frequency counting.</summary>
        public IEqualityComparer<T> Comparer { get; }
        /// <summary>Optional whole-chunk serializer.</summary>
        public GenericSerializer<IChunk2D<T>>? Serializer { get; set; }
        /// <summary>Value returned for cells absent from the sparse dictionary.</summary>
        public T DefaultValue { get; set; }
        /// <summary>Caller-owned metadata carried into converted chunks.</summary>
        public object userdata { get; set; }

        /// <summary>Side length of this square chunk.</summary>
        public int Size { get; set; }

        /// <summary>Creates an initially empty sparse chunk.</summary>
        public SparseChunk2D(int size = 16, T defaultValue = default,
            IEqualityComparer<T>? comparer = null, GenericSerializer<IChunk2D<T>>? serializer = null)
        {
            Size = size;
            DefaultValue = defaultValue;
            Comparer = comparer ?? EqualityComparer<T>.Default;
            Serializer = serializer;
        }

        /// <summary>Reads a local cell, or stores/removes it when assigned a non-default/default value.</summary>
        /// <remarks>This indexer does not check coordinates against Size.</remarks>
        public T this[int x, int y]
        {
            get
            {
                K X = (K)Convert.ChangeType(x, typeof(K));
                K Y = (K)Convert.ChangeType(y, typeof(K));
                return tiles.TryGetValue((X,Y), out Cell cell) ? cell.value : DefaultValue;
            }
            set
            {
                K X = (K)Convert.ChangeType(x, typeof(K));
                K Y = (K)Convert.ChangeType(y, typeof(K));
                if (Comparer.Equals(value, DefaultValue))
                    tiles.TryRemove((X,Y), out _);
                else
                    tiles[(X,Y)] = new Cell(value);
            }
        }

        /// <summary>Enumerates only explicitly stored cells.</summary>
        /// <param name="includeDefault">Currently ignored for sparse 2D chunks.</param>
        public IEnumerable<(int x, int y, T value)> Enumerate(bool includeDefault = false)
        {
            // NOTE: includeDefault is intentionally ignored for sparse chunks (performance).
            foreach (var kv in tiles)
            {
                var k = kv.Key;
                int x = (int)Convert.ChangeType(k.Item1, typeof(int));
                int y = (int)Convert.ChangeType(k.Item2, typeof(int));
                yield return (x, y, kv.Value.value);
            }
        }

        /// <summary>Yields this chunk's stored local cells, filtering in the inner iterator only when exact is true.</summary>
        public IEnumerable<IEnumerable<(int x, int y, T value)>> EnumerateRange(Range2D range, bool exact)
        {
            yield return exact ? EnumerateRangeExact(range) : Enumerate();
        }

        private IEnumerable<(int x, int y, T value)> EnumerateRangeExact(Range2D range)
        {
            foreach (var cell in Enumerate())
                if (range.Contains(cell.x, cell.y)) yield return cell;
        }
        
        /// <summary>Copies this chunk into a dense buffer of the same size.</summary>
        public DenseChunk2D<K, T> ToDenseChunk()
        {
            return ToDenseChunk(Size);
        }

        /// <summary>Copies cells that fit the requested dense buffer.</summary>
        public DenseChunk2D<K, T> ToDenseChunk(int size)
        {
            if (size <= 0 || size < Size) throw new ArgumentOutOfRangeException(nameof(size));
            var denseChunk = new DenseChunk2D<K, T>(size, DefaultValue, Comparer, Serializer);
            denseChunk.userdata = userdata;
            foreach (var kvp in tiles)
            {
                var k = kvp.Key;
                int X = (int)Convert.ChangeType(k.Item1, typeof(int));
                int Y = (int)Convert.ChangeType(k.Item2, typeof(int));
                if ((uint)X >= (uint)size || (uint)Y >= (uint)size) continue;
                denseChunk[X,Y] = kvp.Value.value;
            }
            return denseChunk;
        }

        /// <summary>Optimizes against the most common cell value.</summary>
        public IChunk2D<T>? Optimize() => OptimizeImpl(false, default!);

        /// <summary>Optimizes against an explicit default, including null or default(T).</summary>
        public IChunk2D<T>? Optimize(T defaultValue) => OptimizeImpl(true, defaultValue);

        private IChunk2D<T>? OptimizeImpl(bool hasDefaultValue, T defaultValue)
        {
            int total = checked(Size * Size);
            var frequency = new ValueCounter<T>(Comparer);
            int storedInBounds = 0;
            foreach (var kvp in tiles)
            {
                int x = (int)Convert.ChangeType(kvp.Key.Item1, typeof(int));
                int y = (int)Convert.ChangeType(kvp.Key.Item2, typeof(int));
                if ((uint)x >= (uint)Size || (uint)y >= (uint)Size) continue;
                storedInBounds++;
                frequency.Add(kvp.Value.value);
            }
            frequency.Add(DefaultValue, total - storedInBounds);

            T targetDefault = hasDefaultValue ? defaultValue : frequency.MostCommon(DefaultValue);
            int targetCount = frequency.Count(targetDefault);
            if (storedInBounds != tiles.Count) return this;
            if (targetCount == total && (hasDefaultValue || Comparer.Equals(targetDefault, DefaultValue))) return null;
            if (total - targetCount >= tiles.Count) return this;

            var replacement = new SparseChunk2D<K, T>(Size, targetDefault, Comparer, Serializer);
            replacement.userdata = userdata;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    T value = this[x, y];
                    if (!Comparer.Equals(value, targetDefault)) replacement[x, y] = value;
                }
            return replacement;
        }

        /// <summary>Looks up an explicitly stored cell; returns false for a missing key.</summary>
        /// <remarks>On a miss, value is this chunk's DefaultValue.</remarks>
        public bool TryGet(int x, int y, out T value)
        {
            K kx = (K)Convert.ChangeType(x, typeof(K));
            K ky = (K)Convert.ChangeType(y, typeof(K));
            if (tiles.TryGetValue((kx, ky), out Cell cell))
            {
                value = cell.value;
                return true;
            }
            value = DefaultValue;
            return false;
        }

        /// <summary>Stores a non-default value, or removes a matching-default entry.</summary>
        /// <returns>True for a stored non-default value, or whether an entry was removed for a default value.</returns>
        public bool TrySet(int x, int y, T value)
        {
            K kx = (K)Convert.ChangeType(x, typeof(K));
            K ky = (K)Convert.ChangeType(y, typeof(K));
            if (Comparer.Equals(value, DefaultValue)) return tiles.TryRemove((kx, ky), out _);
            tiles[(kx, ky)] = new Cell(value);
            return true;
        }

        /// <summary>Calls the optional whole-chunk serializer.</summary>
        public virtual byte[] Serialize() => Serializer?.Serialize(this)
            ?? throw new NotSupportedException("No serializer has been supplied for this chunk.");
    }
}
