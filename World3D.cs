using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    public class World3D<K, T> where T : unmanaged where K : unmanaged
    {
        public T defaultValue;

        private readonly Dictionary<(int, int, int), IChunk3D<T>> chunks = new();

        public delegate IChunk3D<T> ChunkFactoryDelegate(Range3D range);
        private readonly ChunkFactoryDelegate chunkFactory;
        private readonly int chunkSize;
        public int ChunkSize => chunkSize;

        public IEnumerable<KeyValuePair<(int x, int y, int z), IChunk3D<T>>> EnumerateChunks()
        {
            foreach (var kv in chunks) yield return kv;
        }

        public bool useBoundChecking;
        public Range3D worldBounds;

        public World3D(ChunkFactoryDelegate factory, int chunkSize = 16)
        {
            this.chunkFactory = factory;
            this.chunkSize = chunkSize;
        }

        private IChunk3D<T> GetChunk(int chunkX, int chunkY, int chunkZ)
        {
            if (!chunks.TryGetValue((chunkX, chunkY, chunkZ), out var chunk))
            {
                chunk = chunkFactory(new Range3D(
                    chunkX * chunkSize, (chunkX + 1) * chunkSize - 1,
                    chunkY * chunkSize, (chunkY + 1) * chunkSize - 1,
                    chunkZ * chunkSize, (chunkZ + 1) * chunkSize - 1));
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
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY, worldZ))) return defaultValue;
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            return GetChunk(chunkX, chunkY, chunkZ)[localX, localY, localZ];
        }

        public void SetTile(int worldX, int worldY, int worldZ, T value)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY, worldZ))) return;
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            GetChunk(chunkX, chunkY, chunkZ)[localX, localY, localZ] = value;
        }

        public bool TryGetTile(int worldX, int worldY, int worldZ, out T value, T defaultValue)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY, worldZ))) { value = defaultValue; return false; }
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
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY, worldZ))) return false;
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            if (!chunks.TryGetValue((chunkX, chunkY, chunkZ), out var chunk)) return false;
            chunk[localX, localY, localZ] = value;
            return true;
        }

        public void RemoveSchunk(int chunkX, int chunkY, int chunkZ)
        {
            chunks.Remove((chunkX, chunkY, chunkZ));
        }

        public void OptimizeChunk(int chunkX, int chunkY, int chunkZ)
        {
            var key = (chunkX, chunkY, chunkZ);
            if (!chunks.TryGetValue(key, out var chunk)) return;
            IChunk3D<T>? optimized;
            if (chunk is DenseChunk3D<K, T> denseChunk)
                optimized = denseChunk.Optimize(defaultValue);
            else if (chunk is SparseChunk3D<K, T> sparseChunk)
                optimized = sparseChunk.Optimize(defaultValue);
            else return;

            if (optimized == null) chunks.Remove(key);
            else if (!ReferenceEquals(optimized, chunk)) chunks[key] = optimized;
        }

        public void OptimizeAllChunks()
        {
            var keys = new List<(int, int, int)>(chunks.Keys);
            foreach (var key in keys) OptimizeChunk(key.Item1, key.Item2, key.Item3);
        }

        public ChunkSerializer3D<K, T> GetSerializer(Dictionary<T, T>? replacements)
        {
            return new ChunkSerializer3D<K, T>(replacements);
        }

        public ChunkBlitSerializer3D<K, T> GetFastSerializer()
        {
            return new ChunkBlitSerializer3D<K, T>();
        }
    }
}
