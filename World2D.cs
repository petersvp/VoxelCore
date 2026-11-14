using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PiDev.VoxelCore
{
    public class World2D<K, T> where T : unmanaged where K : unmanaged
    {
        public bool usePreferredDefaultValue;
        public T defaultValue;

        private readonly Dictionary<(int, int), IChunk2D<T>> chunks = new();

        public delegate IChunk2D<T> ChunkFactoryDelegate(Range2D range);
        private readonly ChunkFactoryDelegate chunkFactory;
        private readonly int chunkSize;
        public int ChunkSize => chunkSize;

        public IEnumerable<KeyValuePair<(int x, int y), IChunk2D<T>>> EnumerateChunks()
        {
            foreach (var kv in chunks) yield return kv;
        }

        public bool useBoundChecking;
        public Range2D worldBounds;

        public World2D(ChunkFactoryDelegate factory, int chunkSize = 16, T? defaultValue = null)
        {
            this.chunkFactory = factory;
            this.chunkSize = chunkSize;
            if (defaultValue.HasValue)
            {
                usePreferredDefaultValue = true;
                this.defaultValue = defaultValue.Value;
            }
            else usePreferredDefaultValue = false;
        }

        private IChunk2D<T> GetChunk(int chunkX, int chunkY)
        {
            if (!chunks.TryGetValue((chunkX, chunkY), out var chunk))
            {
                chunk = chunkFactory(new Range2D(chunkX * chunkSize, (chunkX + 1) * chunkSize - 1, chunkY * chunkSize, (chunkY + 1) * chunkSize - 1));
                chunks[(chunkX, chunkY)] = chunk;
            }
            return chunk;
        }
        public IChunk2D<T> GetChunkAt(int worldX, int worldY)
        {
            (int chunkX, int chunkY, _, _) = GetChunkCoords(worldX, worldY);
            return GetChunk(chunkX, chunkY);
        }

        private (int chunkX, int chunkY, int localX, int localY) GetChunkCoords(int worldX, int worldY)
        {
            int chunkX = Utilities.nfdiv(worldX, chunkSize);
            int chunkY = Utilities.nfdiv(worldY, chunkSize);
            int localX = (int)Utilities.nfmod(worldX, chunkSize);
            int localY = (int)Utilities.nfmod(worldY, chunkSize);
            return (chunkX, chunkY, localX, localY);
        }

        public T this[int worldX, int worldY]
        {
            get => GetTile(worldX, worldY);
            set => SetTile(worldX, worldY, value);
        }

        public T GetTile(int worldX, int worldY)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY))) return defaultValue;
            (int chunkX, int chunkY, int localX, int localY) = GetChunkCoords(worldX, worldY);
            return GetChunk(chunkX, chunkY)[localX, localY];
        }

        public void SetTile(int worldX, int worldY, T value)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY))) return;
            (int chunkX, int chunkY, int localX, int localY) = GetChunkCoords(worldX, worldY);
            GetChunk(chunkX, chunkY)[localX, localY] = value;
        }

        public bool TryGetTile(int worldX, int worldY, out T value, T defaultValue)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY))) { value = defaultValue; return false; }
            (int chunkX, int chunkY, int localX, int localY) = GetChunkCoords(worldX, worldY);
            if (!chunks.TryGetValue((chunkX, chunkY), out var chunk))
            {
                value = defaultValue;
                return false;
            }
            value = chunk[localX, localY];
            return true;
        }

        public bool TrySetTile(int worldX, int worldY, T value)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY))) return false;
            (int chunkX, int chunkY, int localX, int localY) = GetChunkCoords(worldX, worldY);
            if (!chunks.TryGetValue((chunkX, chunkY), out var chunk))
                return false;

            chunk[localX, localY] = value;
            return true;
        }

        public void OptimizeChunk(int chunkX, int chunkY)
        {
            if (!chunks.TryGetValue((chunkX, chunkY), out var chunk)) return;
            if (chunk is DenseChunk2D<K, T> denseChunk)
            {
                var sparseChunk = denseChunk.Optimize() ?? new SparseChunk2D<K, T>(chunkSize, usePreferredDefaultValue ? defaultValue : denseChunk.DefaultValue);
                chunks[(chunkX, chunkY)] = sparseChunk;
            }
        }

        public void OptimizeAllChunks()
        {
            foreach (var kvp in chunks)
            {
                if (kvp.Value is DenseChunk2D<K, T> denseChunk)
                {
                    var sparseChunk = denseChunk.Optimize() ?? new SparseChunk2D<K, T>(chunkSize, usePreferredDefaultValue ? defaultValue : denseChunk.DefaultValue);
                    chunks[(kvp.Key.Item1, kvp.Key.Item2)] = sparseChunk;
                }
            }
        }

        public ChunkSerializer2D<K, T> GetSerializer(Dictionary<T, T>? replacements)
        {
            return new ChunkSerializer2D<K, T>(replacements);
        }

        public ChunkBlitSerializer2D<K, T> GetFastSerializer()
        {
            return new ChunkBlitSerializer2D<K, T>();
        }
    }
}
