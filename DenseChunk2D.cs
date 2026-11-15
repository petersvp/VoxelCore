using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    public unsafe class DenseChunk2D<K, T> : IChunk2D<T> where K : unmanaged where T : unmanaged
    {
        internal T* tiles;
        private readonly int size;
        public T DefaultValue { get; set; }
        public object userdata { get; set; }

        public int Size => size;

        public DenseChunk2D(int size = 16, T defaultValue = default)
        {
            this.size = size;
            DefaultValue = defaultValue;
            tiles = null; // Initialize as null to avoid unnecessary allocations
        }

        private int GetIndex(int x, int y) => (y * size) + x;

        public T this[int x, int y]
        {
            get => (tiles != null) ? tiles[GetIndex(x, y)] : DefaultValue;
            set
            {
                if (tiles == null)
                {
                    tiles = (T*)Marshal.AllocHGlobal(size * size * sizeof(T));
                    for (int i = 0; i < size * size; i++)
                        tiles[i] = DefaultValue;
                }
                tiles[GetIndex(x, y)] = value;
            }
        }

        public Dictionary<T, int> CountValues()
        {
            Dictionary<T, int> valueCounts = new Dictionary<T, int>();
            if (tiles == null) return valueCounts;
            T* end = tiles + (size * size);
            for (T* current = tiles; current < end; current++)
            {
                T value = *current;
                if (valueCounts.ContainsKey(value))
                    valueCounts[value]++;
                else
                    valueCounts[value] = 1;
            }
            return valueCounts;
        }

        public SparseChunk2D<K, T> ToSparseChunk(T defaultValue)
        {
            var sparseChunk = new SparseChunk2D<K, T>(size, defaultValue);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    T value = this[x, y];
                    if (!value.Equals(defaultValue)) sparseChunk[x, y] = value;
                }
            }
            return sparseChunk;
        }

        public SparseChunk2D<K, T>? Optimize()
        {
            if (tiles == null) return null;

            var frequency = new Dictionary<T, int>();
            for (int i = 0; i < size * size; i++)
            {
                T value = tiles[i];
                if (!frequency.ContainsKey(value))
                    frequency[value] = 0;
                frequency[value]++;
            }

            // Find the most common value
            T mostCommonValue = DefaultValue;
            int mostCommonCount = 0;
            foreach (var kvp in frequency)
            {
                if (kvp.Value > mostCommonCount)
                {
                    mostCommonCount = kvp.Value;
                    mostCommonValue = kvp.Key;
                }
            }

            // If the most common value is significantly dominant, convert to SparseChunk
            if (mostCommonCount > (size * size * 0.6))
            {
                var sparseChunk = new SparseChunk2D<K, T>(size, mostCommonValue);
                for (int i = 0; i < size * size; i++)
                {
                    T value = tiles[i];
                    if (!value.Equals(mostCommonValue))
                    {
                        int x = i % size;
                        int y = i / size;
                        sparseChunk[x, y] = value;
                    }
                }
                return sparseChunk;
            }
            return null;
        }

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

        public bool TrySet(int x, int y, T value)
        {
            if (tiles == null)
            {
                tiles = (T*)Marshal.AllocHGlobal(size * size * sizeof(T));
                for (int i = 0; i < size * size; i++)
                    tiles[i] = DefaultValue;
            }
            tiles[GetIndex(x, y)] = value;
            return true;
        }

        public IEnumerable<(int x, int y, T value)> Enumerate(bool includeDefault = false)
        {
            var def = DefaultValue;
            int s = Size;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    var v = this[x, y];
                    if (includeDefault || !v.Equals(def))
                        yield return (x, y, v);
                }
        }

        ~DenseChunk2D()
        {
            if (tiles != null) Marshal.FreeHGlobal((IntPtr)tiles);
        }
    }

}
