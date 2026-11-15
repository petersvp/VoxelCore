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

        public SparseChunk2D<K, T> ToSparseChunk() => ToSparseChunk(DefaultValue);

        public SparseChunk2D<K, T> ToSparseChunk(T defaultValue)
        {
            var sparseChunk = new SparseChunk2D<K, T>(size, defaultValue);
            sparseChunk.userdata = userdata;

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

        public IChunk2D<T>? Optimize(T? defaultValue = null)
        {
            if (tiles == null)
                return !defaultValue.HasValue || defaultValue.Value.Equals(DefaultValue) ? null : this;

            int total = checked(size * size);
            var frequency = CountValues();
            T mostCommonValue = DefaultValue;
            int mostCommonCount = 0;
            foreach (var kvp in frequency)
                if (kvp.Value > mostCommonCount)
                {
                    mostCommonCount = kvp.Value;
                    mostCommonValue = kvp.Key;
                }

            T targetDefault = defaultValue.HasValue ? defaultValue.Value : mostCommonValue;
            frequency.TryGetValue(targetDefault, out int targetCount);
            if (targetCount == total && (defaultValue.HasValue || targetDefault.Equals(DefaultValue))) return null;

            long sparseBytes = (long)(total - targetCount) * (2 * sizeof(K) + sizeof(T));
            long denseBytes = (long)total * sizeof(T);
            if (sparseBytes >= denseBytes) return this;
            return ToSparseChunk(targetDefault);
        }

        ~DenseChunk2D()
        {
            if (tiles != null) Marshal.FreeHGlobal((IntPtr)tiles);
        }
    }

}
