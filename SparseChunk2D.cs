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
            var denseChunk = new DenseChunk2D<K, T>(Size, DefaultValue);
            foreach (var kvp in tiles)
            {
                var k = kvp.Key;
                int X = (int)Convert.ChangeType(k.Item1, typeof(int));
                int Y = (int)Convert.ChangeType(k.Item2, typeof(int));
                denseChunk[X,Y] = kvp.Value;
            }
            return denseChunk;
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
