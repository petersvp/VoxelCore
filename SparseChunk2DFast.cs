using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    /// <summary>A 2D chunk that stores only cells different from its default value.</summary>
    /// <remarks>Assigning the default removes a stored entry. Enumerate yields stored entries only, even with includeDefault=true. Cell operations may run concurrently; callers must coordinate changes to Size or DefaultValue and optimization or conversion with writes.</remarks>
    /// <example><![CDATA[
    /// var chunk = new SparseChunk2DFast<byte, int>(16, 0);
    /// chunk[2, 3] = 7;
    /// chunk[2, 3] = 0; // Removes the entry.
    /// ]]></example>
    public class SparseChunk2DFast<K, T> : IChunk2DFast<T> where T : unmanaged where K : unmanaged
    {
        internal ConcurrentDictionary<(K, K), T> tiles = new();
        /// <summary>Value returned for cells absent from the sparse dictionary.</summary>
        public T DefaultValue { get; set; }
        /// <summary>Caller-owned metadata carried into converted chunks.</summary>
        public object userdata { get; set; }

        /// <summary>Side length of this square chunk.</summary>
        public int Size { get; set; }

        /// <summary>Creates an initially empty sparse chunk.</summary>
        public SparseChunk2DFast(int size = 16, T defaultValue = default)
        {
            Size = size;
            DefaultValue = defaultValue;
        }

        /// <summary>Reads a local cell, or stores/removes it when assigned a non-default/default value.</summary>
        /// <remarks>This indexer does not check coordinates against Size.</remarks>
        public T this[int x, int y]
        {
            get
            {
                K X = (K)Convert.ChangeType(x, typeof(K));
                K Y = (K)Convert.ChangeType(y, typeof(K));
                return tiles.TryGetValue((X,Y), out T value) ? value : DefaultValue;
            }
            set
            {
                K X = (K)Convert.ChangeType(x, typeof(K));
                K Y = (K)Convert.ChangeType(y, typeof(K));
                if (value.Equals(DefaultValue))
                    tiles.TryRemove((X,Y), out _);
                else
                    tiles[(X,Y)] = value;
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
                yield return (x, y, kv.Value);
            }
        }
        
        /// <summary>Copies this chunk into a dense buffer of the same size.</summary>
        public DenseChunk2DFast<K, T> ToDenseChunk()
        {
            return ToDenseChunk(Size);
        }

        /// <summary>Copies this chunk into a dense buffer whose side length is at least Size.</summary>
        public DenseChunk2DFast<K, T> ToDenseChunk(int size)
        {
            if (size <= 0 || size < Size) throw new ArgumentOutOfRangeException(nameof(size));
            var denseChunk = new DenseChunk2DFast<K, T>(size, DefaultValue);
            denseChunk.userdata = userdata;
            foreach (var kvp in tiles)
            {
                var k = kvp.Key;
                int X = (int)Convert.ChangeType(k.Item1, typeof(int));
                int Y = (int)Convert.ChangeType(k.Item2, typeof(int));
                if ((uint)X >= (uint)size || (uint)Y >= (uint)size)
                    throw new ArgumentOutOfRangeException(nameof(size));
                denseChunk[X,Y] = kvp.Value;
            }
            return denseChunk;
        }

        /// <summary>Rebases the sparse default when that reduces the number of stored cells.</summary>
        /// <param name="defaultValue">Target default when supplied; null lets the chunk choose its most common value.</param>
        /// <returns>This if no rebase helps; a new sparse chunk if one helps; null when all cells match a supplied default or the current default.</returns>
        public IChunk2DFast<T>? Optimize(T? defaultValue = null)
        {
            int total = checked(Size * Size);
            var frequency = new Dictionary<T, int>();
            int storedInBounds = 0;
            foreach (var kvp in tiles)
            {
                int x = (int)Convert.ChangeType(kvp.Key.Item1, typeof(int));
                int y = (int)Convert.ChangeType(kvp.Key.Item2, typeof(int));
                if ((uint)x >= (uint)Size || (uint)y >= (uint)Size) continue;
                storedInBounds++;
                frequency.TryGetValue(kvp.Value, out int count);
                frequency[kvp.Value] = count + 1;
            }
            frequency.TryGetValue(DefaultValue, out int defaultCount);
            frequency[DefaultValue] = defaultCount + total - storedInBounds;

            T mostCommonValue = DefaultValue;
            foreach (var kvp in frequency)
                if (kvp.Value > frequency[mostCommonValue])
                    mostCommonValue = kvp.Key;
            T targetDefault = defaultValue.HasValue ? defaultValue.Value : mostCommonValue;
            frequency.TryGetValue(targetDefault, out int targetCount);
            if (targetCount == total && (defaultValue.HasValue || targetDefault.Equals(DefaultValue))) return null;
            if (total - targetCount >= tiles.Count) return this;

            var replacement = new SparseChunk2DFast<K, T>(Size, targetDefault);
            replacement.userdata = userdata;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    T value = this[x, y];
                    if (!value.Equals(targetDefault)) replacement[x, y] = value;
                }
            return replacement;
        }

        /// <summary>Looks up an explicitly stored cell; returns false for a missing key.</summary>
        /// <remarks>On a miss, value is default(T), not necessarily this chunk's DefaultValue. This method's direct key cast currently requires K to be int.</remarks>
        public bool TryGet(int x, int y, out T value)
        {
            K kx = (K)(object)x;
            K ky = (K)(object)y;
            return tiles.TryGetValue((kx, ky), out value);
        }

        /// <summary>Stores a non-default value, or removes a matching-default entry.</summary>
        /// <remarks>This method's direct key cast currently requires K to be int.</remarks>
        /// <returns>True for a stored non-default value, or whether an entry was removed for a default value.</returns>
        public bool TrySet(int x, int y, T value)
        {
            K kx = (K)(object)x;
            K ky = (K)(object)y;
            if (value.Equals(DefaultValue)) return tiles.TryRemove((kx, ky), out _);
            tiles[(kx, ky)] = value;
            return true;
        }
    }
}
