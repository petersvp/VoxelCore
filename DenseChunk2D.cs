using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>A 2D chunk with a lazily allocated managed cell array.</summary>
    /// <remarks>Before the first write, every cell reads as <see cref="DefaultValue"/> and no buffer is allocated.</remarks>
    /// <example><![CDATA[
    /// var chunk = new DenseChunk2D<byte, int>(16, 0);
    /// chunk[2, 3] = 7;
    /// IChunk2D<int>? optimized = chunk.Optimize(); // May be this, a sparse chunk, or null.
    /// ]]></example>
    public class DenseChunk2D<K, T> : IChunk2D<T> where K : unmanaged
    {
        private T[]? tiles;
        private readonly int size;
        /// <summary>Comparer used for default values and frequency counting.</summary>
        public IEqualityComparer<T> Comparer { get; }
        /// <summary>Optional whole-chunk serializer.</summary>
        public GenericSerializer<IChunk2D<T>>? Serializer { get; set; }
        /// <summary>Value read from every cell before a buffer is allocated.</summary>
        public T DefaultValue { get; set; }
        /// <summary>Caller-owned metadata carried into converted chunks.</summary>
        public object userdata { get; set; }

        /// <summary>Side length of this square chunk.</summary>
        public int Size => size;

        /// <summary>Creates a chunk whose buffer is initially unallocated.</summary>
        public DenseChunk2D(int size = 16, T defaultValue = default,
            IEqualityComparer<T>? comparer = null, GenericSerializer<IChunk2D<T>>? serializer = null)
        {
            this.size = size;
            DefaultValue = defaultValue;
            Comparer = comparer ?? EqualityComparer<T>.Default;
            Serializer = serializer;
            tiles = null;
        }

        private int GetIndex(int x, int y) => (y * size) + x;

        /// <summary>Reads or writes a local cell; the first write allocates and fills the buffer.</summary>
        /// <remarks>Coordinates must be within 0 through Size-1; the array access checks its flat index.</remarks>
        public T this[int x, int y]
        {
            get => (tiles != null) ? tiles[GetIndex(x, y)] : DefaultValue;
            set
            {
                if (tiles == null)
                {
                    tiles = new T[checked(size * size)];
                    Array.Fill(tiles, DefaultValue);
                }
                tiles[GetIndex(x, y)] = value;
            }
        }

        /// <summary>Reads a cell; returns false with DefaultValue if the buffer is still unallocated.</summary>
        public bool TryGet(int x, int y, out T value)
        {
            if (tiles == null)
            {
                value = DefaultValue;
                return false;
            }
            value = tiles[GetIndex(x, y)];
            return true;
        }

        /// <summary>Writes a cell, allocating the buffer if needed, and returns true.</summary>
        public bool TrySet(int x, int y, T value)
        {
            if (tiles == null)
            {
                tiles = new T[checked(size * size)];
                Array.Fill(tiles, DefaultValue);
            }
            tiles[GetIndex(x, y)] = value;
            return true;
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

        /// <summary>Enumerates local cells, omitting default-valued cells unless requested.</summary>
        /// <param name="includeDefault">True includes every cell; false yields only values different from DefaultValue.</param>
        public IEnumerable<(int x, int y, T value)> Enumerate(bool includeDefault = false)
        {
            var def = DefaultValue;
            int s = Size;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    var v = this[x, y];
                    if (includeDefault || !Comparer.Equals(v, def))
                        yield return (x, y, v);
                }
        }

        /// <summary>Copies this chunk into sparse storage using its current DefaultValue.</summary>
        public SparseChunk2D<K, T> ToSparseChunk() => ToSparseChunk(DefaultValue);

        /// <summary>Copies this chunk into sparse storage using a specified default value.</summary>
        public SparseChunk2D<K, T> ToSparseChunk(T defaultValue)
        {
            var sparseChunk = new SparseChunk2D<K, T>(size, defaultValue, Comparer, Serializer);
            sparseChunk.userdata = userdata;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    T value = this[x, y];
                    if (!Comparer.Equals(value, defaultValue)) sparseChunk[x, y] = value;
                }
            }
            return sparseChunk;
        }

        /// <summary>Optimizes against the most common cell value.</summary>
        public IChunk2D<T>? Optimize() => OptimizeImpl(false, default!);

        /// <summary>Optimizes against an explicit default, including null or default(T).</summary>
        public IChunk2D<T>? Optimize(T defaultValue) => OptimizeImpl(true, defaultValue);

        private IChunk2D<T>? OptimizeImpl(bool hasDefaultValue, T defaultValue)
        {
            if (tiles == null)
                return !hasDefaultValue || Comparer.Equals(defaultValue, DefaultValue) ? null : this;

            int total = checked(size * size);
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
