using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>A 3D chunk that stores only cells different from its default value.</summary>
    /// <remarks>Assigning the default removes a stored entry. TrySet returns whether it changed storage for a default assignment. Cell operations may run concurrently; callers must coordinate changes to Size or DefaultValue and optimization or conversion with writes.</remarks>
    /// <example><![CDATA[
    /// var chunk = new SparseChunk3DFast<byte, int>(16, 0);
    /// chunk[2, 3, 4] = 7;
    /// IChunk3DFast<int>? optimized = chunk.Optimize(0); // Null means this chunk can be discarded.
    /// ]]></example>
    public class SparseChunk3DFast<K, T> : IChunk3DFast<T> where T : unmanaged where K : unmanaged
    {
        internal ConcurrentDictionary<(K, K, K), T> tiles = new();
        /// <summary>Backing field for DefaultValue.</summary>
        public T defaultValue;
        /// <summary>Value returned for cells absent from the sparse dictionary.</summary>
        public T DefaultValue { get => defaultValue; set => defaultValue = value; }
        /// <summary>Caller-owned metadata carried into converted chunks.</summary>
        public object userdata { get; set; }

        /// <summary>Side length of this cubic chunk.</summary>
        public int Size { get; set; }
        
        /// <summary>Creates an initially empty sparse chunk.</summary>
        public SparseChunk3DFast(int size, T defaultValue)
        {
            Size = size;
            DefaultValue = defaultValue;
        }

        /// <summary>Reads a local cell, or stores/removes it when assigned a non-default/default value.</summary>
        /// <remarks>Coordinates are unrestricted within the range representable by K.</remarks>
        public T this[int x, int y, int z]
        {
            get
            {
                return tiles.TryGetValue((ToKey(x), ToKey(y), ToKey(z)), out T value) ? value : DefaultValue;
            }
            set
            {
                var key = (ToKey(x), ToKey(y), ToKey(z));
                if (value.Equals(DefaultValue)) tiles.TryRemove(key, out _);
                else tiles[key] = value;
            }
        }

        private static K ToKey(int coordinate) => (K)Convert.ChangeType(coordinate, typeof(K));

        private static int ToCoordinate(K key) => (int)Convert.ChangeType(key, typeof(int));

        /// <summary>Looks up an explicitly stored cell at any representable coordinate.</summary>
        public bool TryGet(int x, int y, int z, out T value)
        {
            if (tiles.TryGetValue((ToKey(x), ToKey(y), ToKey(z)), out value)) return true;
            value = DefaultValue;
            return false;
        }

        /// <summary>Stores a non-default value at any representable coordinate, or removes a matching-default entry.</summary>
        /// <returns>True for a stored value or when an entry was removed.</returns>
        public bool TrySet(int x, int y, int z, T value)
        {
            var key = (ToKey(x), ToKey(y), ToKey(z));
            if (value.Equals(DefaultValue)) return tiles.TryRemove(key, out _);
            tiles[key] = value;
            return true;
        }

        /// <summary>Enumerates stored entries, or all cells when includeDefault is true.</summary>
        /// <param name="includeDefault">True includes every cell; false yields dictionary entries only.</param>
        public IEnumerable<(int x, int y, int z, T value)> Enumerate(bool includeDefault = false)
        {
            if (includeDefault)
            {
                for (int x = 0; x < Size; x++)
                    for (int y = 0; y < Size; y++)
                        for (int z = 0; z < Size; z++)
                            yield return (x, y, z, this[x, y, z]);
                yield break;
            }
            foreach (var kvp in tiles)
                yield return (ToCoordinate(kvp.Key.Item1), ToCoordinate(kvp.Key.Item2), ToCoordinate(kvp.Key.Item3), kvp.Value);
        }

        /// <summary>Yields this chunk's stored local cells, filtering in the inner iterator only when exact is true.</summary>
        public IEnumerable<IEnumerable<(int x, int y, int z, T value)>> EnumerateRange(Range3D range, bool exact)
        {
            yield return exact ? EnumerateRangeExact(range) : Enumerate();
        }

        private IEnumerable<(int x, int y, int z, T value)> EnumerateRangeExact(Range3D range)
        {
            foreach (var cell in Enumerate())
                if (range.Contains(cell.x, cell.y, cell.z)) yield return cell;
        }

        /// <summary>Rebases the sparse default when that reduces the number of stored cells.</summary>
        /// <param name="defaultValue">Target default when supplied; null lets the chunk choose its most common value.</param>
        /// <returns>This if no rebase helps; a new sparse chunk if one helps; null when all cells match a supplied default or the current default.</returns>
        public IChunk3DFast<T>? Optimize(T? defaultValue = null)
        {
            int total = checked(Size * Size * Size);
            var frequency = new Dictionary<T, int>();
            int storedInBounds = 0;
            foreach (var entry in tiles)
            {
                int x = ToCoordinate(entry.Key.Item1), y = ToCoordinate(entry.Key.Item2), z = ToCoordinate(entry.Key.Item3);
                if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size) continue;
                storedInBounds++;
                T value = entry.Value;
                frequency.TryGetValue(value, out int count);
                frequency[value] = count + 1;
            }
            frequency.TryGetValue(DefaultValue, out int defaultCount);
            frequency[DefaultValue] = defaultCount + total - storedInBounds;
            T mostCommonValue = DefaultValue;
            foreach (var kvp in frequency)
                if (kvp.Value > frequency[mostCommonValue]) mostCommonValue = kvp.Key;
            T targetDefault = defaultValue.HasValue ? defaultValue.Value : mostCommonValue;
            frequency.TryGetValue(targetDefault, out int targetCount);
            if (storedInBounds != tiles.Count) return this;
            if (targetCount == total && (defaultValue.HasValue || targetDefault.Equals(DefaultValue))) return null;
            if (total - targetCount >= tiles.Count) return this;

            var replacement = new SparseChunk3DFast<K, T>(Size, targetDefault);
            replacement.userdata = userdata;
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    for (int z = 0; z < Size; z++)
                    {
                        T value = this[x, y, z];
                        if (!value.Equals(targetDefault)) replacement[x, y, z] = value;
                    }
            return replacement;
        }

        /// <summary>Copies this chunk into a dense buffer of the same size.</summary>
        public DenseChunk3DFast<K, T> ToDenseChunk()
        {
            return ToDenseChunk(Size);
        }

        /// <summary>Copies this chunk into a dense buffer whose side length is at least Size.</summary>
        public DenseChunk3DFast<K, T> ToDenseChunk(int size)
        {
            if (size < Size) throw new ArgumentOutOfRangeException(nameof(size));
            var denseChunk = new DenseChunk3DFast<K, T>(size, DefaultValue);
            denseChunk.userdata = userdata;
            foreach (var kvp in tiles)
            {
                int x = ToCoordinate(kvp.Key.Item1), y = ToCoordinate(kvp.Key.Item2), z = ToCoordinate(kvp.Key.Item3);
                if ((uint)x < (uint)size && (uint)y < (uint)size && (uint)z < (uint)size)
                    denseChunk[x, y, z] = kvp.Value;
            }
            return denseChunk;
        }
    }
}
