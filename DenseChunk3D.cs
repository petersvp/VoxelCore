using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>A 3D chunk with a lazily allocated managed cell array.</summary>
    /// <remarks>Cells are stored with x varying fastest. A null array represents a uniform default-valued chunk.</remarks>
    /// <example><![CDATA[
    /// var chunk = new DenseChunk3D<byte, int>(16, 0);
    /// chunk[2, 3, 4] = 7;
    /// IChunk3D<int>? optimized = chunk.Optimize(); // May be this, a sparse chunk, or null.
    /// ]]></example>
    public class DenseChunk3D<K, T> : IChunk3D<T> where K : unmanaged
    {
        private T[]? tiles;
        private readonly int size;
        /// <summary>Comparer used for default values and frequency counting.</summary>
        public IEqualityComparer<T> Comparer { get; }
        /// <summary>Optional whole-chunk serializer.</summary>
        public GenericSerializer<IChunk3D<T>>? Serializer { get; set; }
        /// <summary>Value read from every cell before a buffer is allocated.</summary>
        public T DefaultValue { get; set; }
        /// <summary>Caller-owned metadata carried into converted chunks.</summary>
        public object userdata { get; set; }

        /// <summary>Side length of this cubic chunk.</summary>
        public int Size => size;

        /// <summary>Creates a chunk whose buffer is initially unallocated.</summary>
        public DenseChunk3D(int size = 16, T defaultValue = default,
            IEqualityComparer<T>? comparer = null, GenericSerializer<IChunk3D<T>>? serializer = null)
        {
            this.size = size;
            DefaultValue = defaultValue;
            Comparer = comparer ?? EqualityComparer<T>.Default;
            Serializer = serializer;
            tiles = null;
        }

        private int GetIndex(int x, int y, int z) => (z * size * size) + (y * size) + x;

        /// <summary>Reads or writes a local cell; the first write allocates and fills the buffer.</summary>
        /// <remarks>Coordinates must be within 0 through Size-1; the array access checks its flat index.</remarks>
        public T this[int x, int y, int z]
        {
            get => (tiles != null) ? tiles[GetIndex(x, y, z)] : DefaultValue;
            set
            {
                if (tiles == null)
                {
                    tiles = new T[checked(size * size * size)];
                    Array.Fill(tiles, DefaultValue);
                }
                tiles[GetIndex(x, y, z)] = value;
            }
        }

        /// <summary>Counts allocated cell values, including null, with the injected comparer.</summary>
        /// <returns>An empty list while the buffer is null.</returns>
        public List<KeyValuePair<T, int>> CountValues()
        {
            if (tiles == null) return new List<KeyValuePair<T, int>>();
            var counts = new ValueCounter<T>(Comparer);
            foreach (T value in tiles) counts.Add(value);
            return counts.ToList();
        }

        /// <summary>Reads a cell; returns false with DefaultValue if the buffer is still unallocated.</summary>
        public bool TryGet(int x, int y, int z, out T value)
        {
            if (tiles == null)
            {
                value = DefaultValue;
                return false;
            }
            value = tiles[GetIndex(x, y, z)];
            return true;
        }

        /// <summary>Writes a cell, allocating the buffer if needed, and returns true.</summary>
        public bool TrySet(int x, int y, int z, T value)
        {
            if (tiles == null)
            {
                tiles = new T[checked(size * size * size)];
                Array.Fill(tiles, DefaultValue);
            }
            tiles[GetIndex(x, y, z)] = value;
            return true;
        }

        /// <summary>Enumerates local cells, omitting default-valued cells unless requested.</summary>
        /// <param name="includeDefault">True includes every cell; false yields only values different from DefaultValue.</param>
        public IEnumerable<(int x, int y, int z, T value)> Enumerate(bool includeDefault = false)
        {
            for (int z = 0; z < Size; z++)
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        T value = this[x, y, z];
                        if (includeDefault || !Comparer.Equals(value, DefaultValue))
                            yield return (x, y, z, value);
                    }
        }

        /// <summary>Copies this chunk into sparse storage using its current DefaultValue.</summary>
        public SparseChunk3D<K, T> ToSparseChunk() => ToSparseChunk(DefaultValue);

        /// <summary>Copies this chunk into sparse storage using a specified default value.</summary>
        public SparseChunk3D<K, T> ToSparseChunk(T defaultValue)
        {
            var sparseChunk = new SparseChunk3D<K, T>(Size, defaultValue, Comparer, Serializer);
            sparseChunk.userdata = userdata;
            for (int z = 0; z < Size; z++)
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        T value = tiles != null ? tiles[GetIndex(x, y, z)] : DefaultValue;
                        if (!Comparer.Equals(value, defaultValue)) sparseChunk[x, y, z] = value;
                    }
            return sparseChunk;
        }

        /// <summary>Optimizes against the most common cell value.</summary>
        public IChunk3D<T>? Optimize() => OptimizeImpl(false, default!);

        /// <summary>Optimizes against an explicit default, including null or default(T).</summary>
        public IChunk3D<T>? Optimize(T defaultValue) => OptimizeImpl(true, defaultValue);

        private IChunk3D<T>? OptimizeImpl(bool hasDefaultValue, T defaultValue)
        {
            if (tiles == null)
                return !hasDefaultValue || Comparer.Equals(defaultValue, DefaultValue) ? null : this;
            int total = checked(size * size * size);
            var frequency = new ValueCounter<T>(Comparer);
            foreach (T value in tiles) frequency.Add(value);
            T targetDefault = hasDefaultValue ? defaultValue : frequency.MostCommon(DefaultValue);
            int targetCount = frequency.Count(targetDefault);
            if (targetCount == total && (hasDefaultValue || Comparer.Equals(targetDefault, DefaultValue))) return null;

            // A sparse dictionary is worthwhile only when fewer than one quarter of cells need entries.
            if ((long)(total - targetCount) * 4 >= total) return this;
            return ToSparseChunk(targetDefault);
        }

        /// <summary>Calls the optional whole-chunk serializer.</summary>
        public virtual byte[] Serialize() => Serializer?.Serialize(this)
            ?? throw new NotSupportedException("No serializer has been supplied for this chunk.");

    }
}
