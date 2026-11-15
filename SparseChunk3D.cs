using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>A 3D chunk that stores only cells different from its default value.</summary>
    /// <remarks>Assigning the default removes a stored entry. TrySet returns whether it changed storage for a default assignment. Cell operations may run concurrently; callers must coordinate changes to Size or DefaultValue and optimization or conversion with writes.</remarks>
    /// <example><![CDATA[
    /// var chunk = new SparseChunk3D<byte, int>(16, 0);
    /// chunk[2, 3, 4] = 7;
    /// IChunk3D<int>? optimized = chunk.Optimize(0); // Null means this chunk can be discarded.
    /// ]]></example>
    public class SparseChunk3D<K, T> : IChunk3D<T> where K : unmanaged
    {
        private readonly struct Cell
        {
            public readonly T value;
            public Cell(T value) { this.value = value; }
        }

        private readonly ConcurrentDictionary<(K, K, K), Cell> tiles = new();
        /// <summary>Comparer used for default values and frequency counting.</summary>
        public IEqualityComparer<T> Comparer { get; }
        /// <summary>Optional whole-chunk serializer.</summary>
        public GenericSerializer<IChunk3D<T>>? Serializer { get; set; }
        /// <summary>Backing field for DefaultValue.</summary>
        public T defaultValue;
        /// <summary>Value returned for cells absent from the sparse dictionary.</summary>
        public T DefaultValue { get => defaultValue; set => defaultValue = value; }
        /// <summary>Caller-owned metadata carried into converted chunks.</summary>
        public object userdata { get; set; }

        /// <summary>Side length of this cubic chunk.</summary>
        public int Size { get; set; }
        
        /// <summary>Creates an initially empty sparse chunk.</summary>
        public SparseChunk3D(int size, T defaultValue,
            IEqualityComparer<T>? comparer = null, GenericSerializer<IChunk3D<T>>? serializer = null)
        {
            Size = size;
            DefaultValue = defaultValue;
            Comparer = comparer ?? EqualityComparer<T>.Default;
            Serializer = serializer;
        }

        /// <summary>Reads a local cell, or stores/removes it when assigned a non-default/default value.</summary>
        /// <remarks>Coordinates outside 0 through Size-1 throw.</remarks>
        public T this[int x, int y, int z]
        {
            get
            {
                CheckBounds(x, y, z);
                return tiles.TryGetValue((ToKey(x), ToKey(y), ToKey(z)), out Cell cell) ? cell.value : DefaultValue;
            }
            set
            {
                CheckBounds(x, y, z);
                var key = (ToKey(x), ToKey(y), ToKey(z));
                if (Comparer.Equals(value, DefaultValue)) tiles.TryRemove(key, out _);
                else tiles[key] = new Cell(value);
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
            if (tiles.TryGetValue((ToKey(x), ToKey(y), ToKey(z)), out Cell cell))
            {
                value = cell.value;
                return true;
            }
            value = default!;
            return false;
        }

        /// <summary>Stores a non-default in-bounds value, or removes a matching-default entry.</summary>
        /// <returns>False outside bounds; otherwise true for a stored value or when an entry was removed.</returns>
        public bool TrySet(int x, int y, int z, T value)
        {
            if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size) return false;
            var key = (ToKey(x), ToKey(y), ToKey(z));
            if (Comparer.Equals(value, DefaultValue)) return tiles.TryRemove(key, out _);
            tiles[key] = new Cell(value);
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
                yield return (ToCoordinate(kvp.Key.Item1), ToCoordinate(kvp.Key.Item2), ToCoordinate(kvp.Key.Item3), kvp.Value.value);
        }

        /// <summary>Optimizes against the most common cell value.</summary>
        public IChunk3D<T>? Optimize() => OptimizeImpl(false, default!);

        /// <summary>Optimizes against an explicit default, including null or default(T).</summary>
        public IChunk3D<T>? Optimize(T defaultValue) => OptimizeImpl(true, defaultValue);

        private IChunk3D<T>? OptimizeImpl(bool hasDefaultValue, T defaultValue)
        {
            int total = checked(Size * Size * Size);
            var frequency = new ValueCounter<T>(Comparer);
            int storedInBounds = 0;
            foreach (var entry in tiles)
            {
                int x = ToCoordinate(entry.Key.Item1);
                int y = ToCoordinate(entry.Key.Item2);
                int z = ToCoordinate(entry.Key.Item3);
                if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size) continue;
                storedInBounds++;
                frequency.Add(entry.Value.value);
            }
            frequency.Add(DefaultValue, total - storedInBounds);
            T targetDefault = hasDefaultValue ? defaultValue : frequency.MostCommon(DefaultValue);
            int targetCount = frequency.Count(targetDefault);
            if (targetCount == total && (hasDefaultValue || Comparer.Equals(targetDefault, DefaultValue))) return null;
            if (total - targetCount >= tiles.Count) return this;

            var replacement = new SparseChunk3D<K, T>(Size, targetDefault, Comparer, Serializer);
            replacement.userdata = userdata;
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    for (int z = 0; z < Size; z++)
                    {
                        T value = this[x, y, z];
                        if (!Comparer.Equals(value, targetDefault)) replacement[x, y, z] = value;
                    }
            return replacement;
        }

        /// <summary>Copies this chunk into a dense buffer of the same size.</summary>
        public DenseChunk3D<K, T> ToDenseChunk()
        {
            var denseChunk = new DenseChunk3D<K, T>(Size, DefaultValue, Comparer, Serializer);
            denseChunk.userdata = userdata;
            foreach (var kvp in tiles)
                denseChunk[ToCoordinate(kvp.Key.Item1), ToCoordinate(kvp.Key.Item2), ToCoordinate(kvp.Key.Item3)] = kvp.Value.value;
            return denseChunk;
        }

        /// <summary>Copies this chunk into a dense buffer whose side length is at least Size.</summary>
        public DenseChunk3D<K, T> ToDenseChunk(int size)
        {
            if (size < Size) throw new ArgumentOutOfRangeException(nameof(size));
            var denseChunk = new DenseChunk3D<K, T>(size, DefaultValue, Comparer, Serializer);
            denseChunk.userdata = userdata;
            foreach (var kvp in tiles)
                denseChunk[ToCoordinate(kvp.Key.Item1), ToCoordinate(kvp.Key.Item2), ToCoordinate(kvp.Key.Item3)] = kvp.Value.value;
            return denseChunk;
        }

        /// <summary>Calls the optional whole-chunk serializer.</summary>
        public virtual byte[] Serialize() => Serializer?.Serialize(this)
            ?? throw new NotSupportedException("No serializer has been supplied for this chunk.");
    }
}
