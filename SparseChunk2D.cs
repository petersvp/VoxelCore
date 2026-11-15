using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{

    public class SparseChunk2D<K, T> : IChunk2D<T> where T : unmanaged where K : unmanaged
    {
        internal Dictionary<(K, K), T> tiles = new();
        public T DefaultValue { get; set; }
        public object userdata { get; set; }

        public int Size { get; set; }

        public SparseChunk2D(int size = 16, T defaultValue = default)
        {
            Size = size;
            DefaultValue = defaultValue;
        }

        public T this[int x, int y]
        {
            get
            {
                K X = (K)Convert.ChangeType(x, typeof(K));
                K Y = (K)Convert.ChangeType(y, typeof(K));
                return tiles.TryGetValue((X,Y), out T value) ? value : DefaultValue;
            }
            set
            {
                K X = (K)Convert.ChangeType(x, typeof(K));
                K Y = (K)Convert.ChangeType(y, typeof(K));
                if (value.Equals(DefaultValue))
                    tiles.Remove((X,Y));
                else
                    tiles[(X,Y)] = value;
            }
        }

        public IEnumerable<(int x, int y, T value)> Enumerate(bool includeDefault = false)
        {
            // NOTE: includeDefault is intentionally ignored for sparse chunks (performance).
            foreach (var kv in tiles)
            {
                var k = kv.Key;
                int x = (int)Convert.ChangeType(k.Item1, typeof(int));
                int y = (int)Convert.ChangeType(k.Item2, typeof(int));
                yield return (x, y, kv.Value);
            }
        }
        
        public DenseChunk2D<K, T> ToDenseChunk()
        {
            return ToDenseChunk(Size);
        }

        public DenseChunk2D<K, T> ToDenseChunk(int size)
        {
            if (size <= 0 || size < Size) throw new ArgumentOutOfRangeException(nameof(size));
            var denseChunk = new DenseChunk2D<K, T>(size, DefaultValue);
            denseChunk.userdata = userdata;
            foreach (var kvp in tiles)
            {
                var k = kvp.Key;
                int X = (int)Convert.ChangeType(k.Item1, typeof(int));
                int Y = (int)Convert.ChangeType(k.Item2, typeof(int));
                if ((uint)X >= (uint)size || (uint)Y >= (uint)size)
                    throw new ArgumentOutOfRangeException(nameof(size));
                denseChunk[X,Y] = kvp.Value;
            }
            return denseChunk;
        }

        public IChunk2D<T>? Optimize(T? defaultValue = null)
        {
            int total = checked(Size * Size);
            var frequency = new Dictionary<T, int>();
            int storedInBounds = 0;
            foreach (var kvp in tiles)
            {
                int x = (int)Convert.ChangeType(kvp.Key.Item1, typeof(int));
                int y = (int)Convert.ChangeType(kvp.Key.Item2, typeof(int));
                if ((uint)x >= (uint)Size || (uint)y >= (uint)Size) continue;
                storedInBounds++;
                frequency.TryGetValue(kvp.Value, out int count);
                frequency[kvp.Value] = count + 1;
            }
            frequency.TryGetValue(DefaultValue, out int defaultCount);
            frequency[DefaultValue] = defaultCount + total - storedInBounds;

            T mostCommonValue = DefaultValue;
            foreach (var kvp in frequency)
                if (kvp.Value > frequency[mostCommonValue])
                    mostCommonValue = kvp.Key;
            T targetDefault = defaultValue.HasValue ? defaultValue.Value : mostCommonValue;
            frequency.TryGetValue(targetDefault, out int targetCount);
            if (targetCount == total && (defaultValue.HasValue || targetDefault.Equals(DefaultValue))) return null;
            if (total - targetCount >= tiles.Count) return this;

            var replacement = new SparseChunk2D<K, T>(Size, targetDefault);
            replacement.userdata = userdata;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    T value = this[x, y];
                    if (!value.Equals(targetDefault)) replacement[x, y] = value;
                }
            return replacement;
        }

        public bool TryGet(int x, int y, out T value)
        {
            K kx = (K)(object)x;
            K ky = (K)(object)y;
            return tiles.TryGetValue((kx, ky), out value);
        }

        public bool TrySet(int x, int y, T value)
        {
            K kx = (K)(object)x;
            K ky = (K)(object)y;
            if (value.Equals(DefaultValue)) return tiles.Remove((kx, ky));
            tiles[(kx, ky)] = value;
            return true;
        }
    }
}
