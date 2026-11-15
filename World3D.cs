using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    public class World3D<K, T> where T : unmanaged where K : unmanaged
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
