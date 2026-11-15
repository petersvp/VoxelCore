using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PiDev.VoxelCore
{
    /// <summary>A 3D chunk with a lazily allocated, contiguous unmanaged cell buffer.</summary>
    /// <remarks>Cells are stored with x varying fastest. A null buffer represents a uniform default-valued chunk.</remarks>
    /// <example><![CDATA[
    /// var chunk = new DenseChunk3DFast<byte, int>(16, 0);
    /// chunk[2, 3, 4] = 7;
    /// IChunk3DFast<int>? optimized = chunk.Optimize(); // May be this, a sparse chunk, or null.
    /// ]]></example>
    public unsafe class DenseChunk3DFast<K, T> : IChunk3DFast<T> where K : unmanaged where T : unmanaged
    {
        internal T* tiles;
        private readonly int size;
        /// <summary>Value read from every cell before a buffer is allocated.</summary>
        public T DefaultValue { get; set; }
        /// <summary>Caller-owned metadata carried into converted chunks.</summary>
        public object userdata { get; set; }

        /// <summary>Side length of this cubic chunk.</summary>
        public int Size => size;

        /// <summary>Creates a chunk whose buffer is initially unallocated.</summary>
        public DenseChunk3DFast(int size = 16, T defaultValue = default)
        {
            this.size = size;
            DefaultValue = defaultValue;
            tiles = null;
        }

        private int GetIndex(int x, int y, int z) => (z * size * size) + (y * size) + x;

        /// <summary>Reads or writes a local cell; the first write allocates and fills the buffer.</summary>
        /// <remarks>Coordinates must be within 0 through Size-1; this pointer-backed indexer does not check bounds.</remarks>
        public T this[int x, int y, int z]
        {
            get => (tiles != null) ? tiles[GetIndex(x, y, z)] : DefaultValue;
            set
            {
                if (tiles == null)
                {
                    tiles = (T*)Marshal.AllocHGlobal(checked(size * size * size * sizeof(T)));
                    for (int i = 0; i < size * size * size; i++)
                        tiles[i] = DefaultValue;
                }
                tiles[GetIndex(x, y, z)] = value;
            }
        }

        /// <summary>Counts all allocated cell values; returns an empty map while the buffer is null.</summary>
        public Dictionary<T, int> CountValues()
        {
            Dictionary<T, int> valueCounts = new Dictionary<T, int>();
            if (tiles == null) return valueCounts;
            T* end = tiles + (size * size * size);
            for (T* current = tiles; current < end; current++)
            {
                T value = *current;
                if (valueCounts.ContainsKey(value)) valueCounts[value]++;
                else valueCounts[value] = 1;
            }
            return valueCounts;
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
                tiles = (T*)Marshal.AllocHGlobal(checked(size * size * size * sizeof(T)));
                for (int i = 0; i < size * size * size; i++)
                    tiles[i] = DefaultValue;
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
                        if (includeDefault || !value.Equals(DefaultValue))
                            yield return (x, y, z, value);
                    }
        }

        /// <summary>Copies this chunk into sparse storage using its current DefaultValue.</summary>
        public SparseChunk3DFast<K, T> ToSparseChunk() => ToSparseChunk(DefaultValue);

        /// <summary>Copies this chunk into sparse storage using a specified default value.</summary>
        public SparseChunk3DFast<K, T> ToSparseChunk(T defaultValue)
        {
            var sparseChunk = new SparseChunk3DFast<K, T>(Size, defaultValue);
            sparseChunk.userdata = userdata;
            for (int z = 0; z < Size; z++)
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        T value = tiles != null ? tiles[GetIndex(x, y, z)] : DefaultValue;
                        if (!value.Equals(defaultValue)) sparseChunk[x, y, z] = value;
                    }
            return sparseChunk;
        }

        /// <summary>Chooses sparse storage when it is smaller than the dense cell buffer.</summary>
        /// <param name="defaultValue">Target default when supplied; null lets this chunk choose its most common value.</param>
        /// <returns>This if conversion is not smaller; a new sparse chunk if it is; null when every cell matches a supplied default, or an empty chunk already matches its own default.</returns>
        public IChunk3DFast<T>? Optimize(T? defaultValue = null)
        {
            if (tiles == null)
                return !defaultValue.HasValue || defaultValue.Value.Equals(DefaultValue) ? null : this;
            int total = checked(size * size * size);
            var frequency = CountValues();

            T mostCommonValue = DefaultValue;
            int mostCommonCount = 0;
            foreach (var kvp in frequency)
                if (kvp.Value > mostCommonCount)
                {
                    mostCommonValue = kvp.Key;
                    mostCommonCount = kvp.Value;
                }

            T targetDefault = defaultValue.HasValue ? defaultValue.Value : mostCommonValue;
            frequency.TryGetValue(targetDefault, out int targetCount);
            if (targetCount == total && (defaultValue.HasValue || targetDefault.Equals(DefaultValue))) return null;

            long sparseBytes = (long)(total - targetCount) * (3 * sizeof(K) + sizeof(T));
            long denseBytes = (long)total * sizeof(T);
            if (sparseBytes >= denseBytes) return this;
            return ToSparseChunk(targetDefault);
        }

        ~DenseChunk3DFast()
        {
            if (tiles != null) Marshal.FreeHGlobal((IntPtr)tiles);
        }
    }
}
