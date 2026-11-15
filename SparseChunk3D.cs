using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>A 3D chunk that stores only cells different from its default value.</summary>
    /// <remarks>Assigning the default removes a stored entry. TrySet returns whether it changed storage for a default assignment.</remarks>
    /// <example><![CDATA[
    /// var chunk = new SparseChunk3D<byte, int>(16, 0);
    /// chunk[2, 3, 4] = 7;
    /// IChunk3D<int>? optimized = chunk.Optimize(0); // Null means this chunk can be discarded.
    /// ]]></example>
    public class SparseChunk3D<K, T> : IChunk3D<T> where T : unmanaged where K : unmanaged
    {
        internal Dictionary<(K, K, K), T> tiles = new();
        /// <summary>Backing field for DefaultValue.</summary>
        public T defaultValue;
        /// <summary>Value returned for cells absent from the sparse dictionary.</summary>
        public T DefaultValue { get => defaultValue; set => defaultValue = value; }
        /// <summary>Caller-owned metadata carried into converted chunks.</summary>
        public object userdata { get; set; }

        /// <summary>Side length of this cubic chunk.</summary>
        public int Size { get; set; }
        
        /// <summary>Creates an initially empty sparse chunk.</summary>
        public SparseChunk3D(int size, T defaultValue)
        {
            Size = size;
            DefaultValue = defaultValue;
        }

        /// <summary>Reads a local cell, or stores/removes it when assigned a non-default/default value.</summary>
        /// <remarks>Coordinates outside 0 through Size-1 throw.</remarks>
        public T this[int x, int y, int z]
        {
            get
            {
                CheckBounds(x, y, z);
                return tiles.TryGetValue((ToKey(x), ToKey(y), ToKey(z)), out T value) ? value : DefaultValue;
            }
            set
            {
                CheckBounds(x, y, z);
                var key = (ToKey(x), ToKey(y), ToKey(z));
                if (value.Equals(DefaultValue)) tiles.Remove(key);
                else tiles[key] = value;
            }
        }

        private static K ToKey(int coordinate) => (K)Convert.ChangeType(coordinate, typeof(K));

        private static int ToCoordinate(K key) => (int)Convert.ChangeType(key, typeof(int));

        private void CheckBounds(int x, int y, int z)
        {
            if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size)
                throw new IndexOutOfRangeException();
        }

        /// <summary>Looks up an explicitly stored in-bounds cell; returns false on a miss or outside bounds.</summary>
        /// <remarks>Outside bounds, value is DefaultValue. An in-bounds missing key yields default(T).</remarks>
        public bool TryGet(int x, int y, int z, out T value)
        {
            if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size)
            {
                value = DefaultValue;
                return false;
            }
            return tiles.TryGetValue((ToKey(x), ToKey(y), ToKey(z)), out value);
        }

        /// <summary>Stores a non-default in-bounds value, or removes a matching-default entry.</summary>
        /// <returns>False outside bounds; otherwise true for a stored value or when an entry was removed.</returns>
        public bool TrySet(int x, int y, int z, T value)
        {
            if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size) return false;
            var key = (ToKey(x), ToKey(y), ToKey(z));
            if (value.Equals(DefaultValue)) return tiles.Remove(key);
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

        /// <summary>Rebases the sparse default when that reduces the number of stored cells.</summary>
        /// <param name="defaultValue">Target default when supplied; null lets the chunk choose its most common value.</param>
        /// <returns>This if no rebase helps; a new sparse chunk if one helps; null when all cells match a supplied default or the current default.</returns>
        public IChunk3D<T>? Optimize(T? defaultValue = null)
        {
            int total = checked(Size * Size * Size);
            var frequency = new Dictionary<T, int> { [DefaultValue] = total - tiles.Count };
            foreach (var value in tiles.Values)
            {
                frequency.TryGetValue(value, out int count);
                frequency[value] = count + 1;
            }
            T mostCommonValue = DefaultValue;
            foreach (var kvp in frequency)
                if (kvp.Value > frequency[mostCommonValue]) mostCommonValue = kvp.Key;
            T targetDefault = defaultValue.HasValue ? defaultValue.Value : mostCommonValue;
            frequency.TryGetValue(targetDefault, out int targetCount);
            if (targetCount == total && (defaultValue.HasValue || targetDefault.Equals(DefaultValue))) return null;
            if (total - targetCount >= tiles.Count) return this;

            var replacement = new SparseChunk3D<K, T>(Size, targetDefault);
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
        public DenseChunk3D<K, T> ToDenseChunk()
        {
            var denseChunk = new DenseChunk3D<K, T>(Size, DefaultValue);
            denseChunk.userdata = userdata;
            foreach (var kvp in tiles)
                denseChunk[ToCoordinate(kvp.Key.Item1), ToCoordinate(kvp.Key.Item2), ToCoordinate(kvp.Key.Item3)] = kvp.Value;
            return denseChunk;
        }

        /// <summary>Copies this chunk into a dense buffer whose side length is at least Size.</summary>
        public DenseChunk3D<K, T> ToDenseChunk(int size)
        {
            if (size < Size) throw new ArgumentOutOfRangeException(nameof(size));
            var denseChunk = new DenseChunk3D<K, T>(size, DefaultValue);
            denseChunk.userdata = userdata;
            foreach (var kvp in tiles)
                denseChunk[ToCoordinate(kvp.Key.Item1), ToCoordinate(kvp.Key.Item2), ToCoordinate(kvp.Key.Item3)] = kvp.Value;
            return denseChunk;
        }
    }
}
