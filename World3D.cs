using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    public class SparseChunk3D<T> : IChunk3D<T> where T : unmanaged
    {
        internal Dictionary<(int, int, int), T> tiles = new();
        public int Size { get; }
        public T defaultValue;
        public T DefaultValue { get => defaultValue; set => defaultValue = value; }
        public object userdata { get; set; }

        public SparseChunk3D(int size, T defaultValue)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            DefaultValue = defaultValue;
        }

        public SparseChunk3D(T defaultValue) : this(16, defaultValue) { }

        public SparseChunk3D() : this(16, default) { }

        public T this[int x, int y, int z]
        {
            get
            {
                CheckBounds(x, y, z);
                return tiles.TryGetValue((x, y, z), out T value) ? value : DefaultValue;
            }
            set
            {
                CheckBounds(x, y, z);
                if (value.Equals(DefaultValue)) tiles.Remove((x, y, z));
                else tiles[(x, y, z)] = value;
            }
        }

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
            return tiles.TryGetValue((x, y, z), out value);
        }

        public bool TrySet(int x, int y, int z, T value)
        {
            if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size) return false;
            this[x, y, z] = value;
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
                yield return (kvp.Key.Item1, kvp.Key.Item2, kvp.Key.Item3, kvp.Value);
        }

        public void Optimize()
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
            if (mostCommonValue.Equals(DefaultValue)) return;

            var replacement = new Dictionary<(int, int, int), T>();
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    for (int z = 0; z < Size; z++)
                    {
                        T value = this[x, y, z];
                        if (!value.Equals(mostCommonValue)) replacement[(x, y, z)] = value;
                    }
            tiles = replacement;
            DefaultValue = mostCommonValue;
        }

        public DenseChunk3D<T> ToDenseChunk()
        {
            var denseChunk = new DenseChunk3D<T>(Size, DefaultValue);
            foreach (var kvp in tiles)
                denseChunk[kvp.Key.Item1, kvp.Key.Item2, kvp.Key.Item3] = kvp.Value;
            return denseChunk;
        }

        public DenseChunk3D<T> ToDenseChunk(int size)
        {
            var denseChunk = new DenseChunk3D<T>(size, DefaultValue);
            foreach (var kvp in tiles)
                denseChunk[kvp.Key.Item1, kvp.Key.Item2, kvp.Key.Item3] = kvp.Value;
            return denseChunk;
        }
    }

    public class DenseChunk3D<T> : IChunk3D<T> where T : unmanaged
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

        public SparseChunk3D<T> ToSparseChunk() => ToSparseChunk(DefaultValue);

        public SparseChunk3D<T> ToSparseChunk(T defaultValue)
        {
            var sparseChunk = new SparseChunk3D<T>(Size, defaultValue);
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    for (int z = 0; z < Size; z++)
                    {
                        T value = tiles[x, y, z];
                        if (!value.Equals(defaultValue)) sparseChunk[x, y, z] = value;
                    }
            return sparseChunk;
        }
    }

    public class World3D<T> where T : unmanaged
    {
        private readonly Dictionary<(int, int, int), IChunk3D<T>> chunks = new();
        private readonly Func<IChunk3D<T>> chunkFactory;
        private readonly int chunkSize;
        public int ChunkSize => chunkSize;

        public World3D(Func<IChunk3D<T>> factory, int chunkSize = 16)
        {
            this.chunkFactory = factory;
            this.chunkSize = chunkSize;
        }

        public IEnumerable<KeyValuePair<(int x, int y, int z), IChunk3D<T>>> EnumerateChunks()
        {
            foreach (var kv in chunks) yield return kv;
        }

        private IChunk3D<T> GetChunk(int chunkX, int chunkY, int chunkZ)
        {
            if (!chunks.TryGetValue((chunkX, chunkY, chunkZ), out var chunk))
            {
                chunk = chunkFactory();
                chunks[(chunkX, chunkY, chunkZ)] = chunk;
            }
            return chunk;
        }

        public IChunk3D<T> GetChunkAt(int worldX, int worldY, int worldZ)
        {
            (int chunkX, int chunkY, int chunkZ, _, _, _) = GetChunkCoords(worldX, worldY, worldZ);
            return GetChunk(chunkX, chunkY, chunkZ);
        }

        private (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) GetChunkCoords(int worldX, int worldY, int worldZ)
        {
            int chunkX = worldX / chunkSize;
            int chunkY = worldY / chunkSize;
            int chunkZ = worldZ / chunkSize;
            int localX = worldX % chunkSize;
            int localY = worldY % chunkSize;
            int localZ = worldZ % chunkSize;

            if (localX < 0) { chunkX -= 1; localX += chunkSize; }
            if (localY < 0) { chunkY -= 1; localY += chunkSize; }
            if (localZ < 0) { chunkZ -= 1; localZ += chunkSize; }

            return (chunkX, chunkY, chunkZ, localX, localY, localZ);
        }

        public T this[int worldX, int worldY, int worldZ]
        {
            get => GetTile(worldX, worldY, worldZ);
            set => SetTile(worldX, worldY, worldZ, value);
        }

        public T GetTile(int worldX, int worldY, int worldZ)
        {
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            return GetChunk(chunkX, chunkY, chunkZ)[localX, localY, localZ];
        }

        public void SetTile(int worldX, int worldY, int worldZ, T value)
        {
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            GetChunk(chunkX, chunkY, chunkZ)[localX, localY, localZ] = value;
        }

        public bool TryGetTile(int worldX, int worldY, int worldZ, out T value, T defaultValue)
        {
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            if (!chunks.TryGetValue((chunkX, chunkY, chunkZ), out var chunk))
            {
                value = defaultValue;
                return false;
            }
            value = chunk[localX, localY, localZ];
            return true;
        }

        public bool TrySetTile(int worldX, int worldY, int worldZ, T value)
        {
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            if (!chunks.TryGetValue((chunkX, chunkY, chunkZ), out var chunk)) return false;
            chunk[localX, localY, localZ] = value;
            return true;
        }

        public void UnloadChunk(int chunkX, int chunkY, int chunkZ)
        {
            chunks.Remove((chunkX, chunkY, chunkZ));
        }

        public ChunkSerializer3D<T> GetSerializer(Dictionary<T, T>? replacements)
        {
            return new ChunkSerializer3D<T>(replacements);
        }

        public ChunkBlitSerializer3D<T> GetFastSerializer()
        {
            return new ChunkBlitSerializer3D<T>();
        }
    }
}
