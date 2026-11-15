using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    public class DenseChunk3D<K, T> : IChunk3D<T> where K : unmanaged where T : unmanaged
    {
        internal readonly T[,,] tiles;
        public int Size { get; }
        public T DefaultValue { get; set; }
        public object userdata { get; set; }

        public DenseChunk3D(int size = 16, T defaultValue = default)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            DefaultValue = defaultValue;
            tiles = new T[size, size, size];
            if (!defaultValue.Equals(default(T)))
                for (int x = 0; x < size; x++)
                    for (int y = 0; y < size; y++)
                        for (int z = 0; z < size; z++)
                            tiles[x, y, z] = defaultValue;
        }

        public T this[int x, int y, int z]
        {
            get => tiles[x, y, z];
            set => tiles[x, y, z] = value;
        }

        public bool TryGet(int x, int y, int z, out T value)
        {
            if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size)
            {
                value = DefaultValue;
                return false;
            }
            value = tiles[x, y, z];
            return true;
        }

        public bool TrySet(int x, int y, int z, T value)
        {
            if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size) return false;
            tiles[x, y, z] = value;
            return true;
        }

        public IEnumerable<(int x, int y, int z, T value)> Enumerate(bool includeDefault = false)
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    for (int z = 0; z < Size; z++)
                    {
                        T value = tiles[x, y, z];
                        if (includeDefault || !value.Equals(DefaultValue))
                            yield return (x, y, z, value);
                    }
        }

        public SparseChunk3D<K, T> ToSparseChunk() => ToSparseChunk(DefaultValue);

        public SparseChunk3D<K, T> ToSparseChunk(T defaultValue)
        {
            var sparseChunk = new SparseChunk3D<K, T>(Size, defaultValue);
            sparseChunk.userdata = userdata;
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    for (int z = 0; z < Size; z++)
                    {
                        T value = tiles[x, y, z];
                        if (!value.Equals(defaultValue)) sparseChunk[x, y, z] = value;
                    }
            return sparseChunk;
        }

        public unsafe SparseChunk3D<K, T>? Optimize()
        {
            int total = checked(Size * Size * Size);
            var frequency = new Dictionary<T, int>();
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    for (int z = 0; z < Size; z++)
                    {
                        T value = tiles[x, y, z];
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
    }
}
