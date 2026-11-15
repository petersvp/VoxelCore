using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    public class SparseChunk3D<K, T> : IChunk3D<T> where T : unmanaged where K : unmanaged
    {
        internal Dictionary<(K, K, K), T> tiles = new();
        public T defaultValue;
        public T DefaultValue { get => defaultValue; set => defaultValue = value; }
        public object userdata { get; set; }

        public int Size { get; set; }
        
        public SparseChunk3D(int size, T defaultValue)
        {
            Size = size;
            DefaultValue = defaultValue;
        }

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

        public bool TryGet(int x, int y, int z, out T value)
        {
            if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size)
            {
                value = DefaultValue;
                return false;
            }
            return tiles.TryGetValue((ToKey(x), ToKey(y), ToKey(z)), out value);
        }

        public bool TrySet(int x, int y, int z, T value)
        {
            if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size) return false;
            var key = (ToKey(x), ToKey(y), ToKey(z));
            if (value.Equals(DefaultValue)) return tiles.Remove(key);
            tiles[key] = value;
            return true;
        }

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

        public DenseChunk3D<K, T> ToDenseChunk()
        {
            var denseChunk = new DenseChunk3D<K, T>(Size, DefaultValue);
            denseChunk.userdata = userdata;
            foreach (var kvp in tiles)
                denseChunk[ToCoordinate(kvp.Key.Item1), ToCoordinate(kvp.Key.Item2), ToCoordinate(kvp.Key.Item3)] = kvp.Value;
            return denseChunk;
        }

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
