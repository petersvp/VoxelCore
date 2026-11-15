using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PiDev.VoxelCore
{
    public unsafe class DenseChunk3D<K, T> : IChunk3D<T> where K : unmanaged where T : unmanaged
    {
        internal T* tiles;
        private readonly int size;
        public T DefaultValue { get; set; }
        public object userdata { get; set; }

        public int Size => size;

        public DenseChunk3D(int size = 16, T defaultValue = default)
        {
            this.size = size;
            DefaultValue = defaultValue;
            tiles = null;
        }

        private int GetIndex(int x, int y, int z) => (z * size * size) + (y * size) + x;

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

        public SparseChunk3D<K, T> ToSparseChunk() => ToSparseChunk(DefaultValue);

        public SparseChunk3D<K, T> ToSparseChunk(T defaultValue)
        {
            var sparseChunk = new SparseChunk3D<K, T>(Size, defaultValue);
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

        public SparseChunk3D<K, T>? Optimize()
        {
            if (tiles == null) return null;
            int total = checked(size * size * size);
            var frequency = new Dictionary<T, int>();
            for (int i = 0; i < total; i++)
            {
                T value = tiles[i];
                frequency.TryGetValue(value, out int count);
                frequency[value] = count + 1;
            }

            T mostCommonValue = DefaultValue;
            int mostCommonCount = 0;
            foreach (var kvp in frequency)
                if (kvp.Value > mostCommonCount)
                {
                    mostCommonValue = kvp.Key;
                    mostCommonCount = kvp.Value;
                }

            long sparseBytes = (long)(total - mostCommonCount) * (3 * sizeof(K) + sizeof(T));
            long denseBytes = (long)total * sizeof(T);
            if (mostCommonCount * 5L <= total * 3L || sparseBytes >= denseBytes) return null;
            return ToSparseChunk(mostCommonValue);
        }

        ~DenseChunk3D()
        {
            if (tiles != null) Marshal.FreeHGlobal((IntPtr)tiles);
        }
    }
}
