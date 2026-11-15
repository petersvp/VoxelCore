using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PiDev.VoxelCore
{
    /// <summary>A lazily created grid of 2D chunks addressed by world coordinates.</summary>
    /// <example><![CDATA[
    /// var world = new World2D<byte, int>(_ => new SparseChunk2D<byte, int>(16, 0));
    /// world[-1, 3] = 7;
    /// foreach (var entry in world.EnumerateChunks())
    /// {
    ///     byte[] data = world.GetSerializer(null).SerializeOptimal(entry.Value, true);
    /// }
    /// ]]></example>
    public class World2D<K, T> where T : unmanaged where K : unmanaged
    {
        /// <summary>Records whether a default value was supplied to the constructor.</summary>
        public bool usePreferredDefaultValue;
        /// <summary>Value returned outside bounds and passed to chunk optimization.</summary>
        public T defaultValue;

        private readonly Dictionary<(int, int), IChunk2D<T>> chunks = new();

        /// <summary>Creates a chunk for the supplied inclusive world-space range.</summary>
        public delegate IChunk2D<T> ChunkFactoryDelegate(Range2D range);
        private readonly ChunkFactoryDelegate chunkFactory;
        private readonly int chunkSize;
        /// <summary>Side length of each square chunk.</summary>
        public int ChunkSize => chunkSize;

        /// <summary>Enumerates chunks already created by this world and their chunk-coordinate keys.</summary>
        public IEnumerable<KeyValuePair<(int x, int y), IChunk2D<T>>> EnumerateChunks()
        {
            foreach (var kv in chunks) yield return kv;
        }

        /// <summary>Whether tile reads and writes are limited to worldBounds.</summary>
        public bool useBoundChecking;
        /// <summary>Inclusive bounds applied when useBoundChecking is true.</summary>
        public Range2D worldBounds;

        /// <summary>Creates a world that produces chunks on first access.</summary>
        /// <param name="factory">Receives the new chunk's inclusive world-space range.</param>
        /// <param name="chunkSize">Side length used for coordinate mapping.</param>
        /// <param name="defaultValue">Optional world default; when omitted, default(T) is used. The stored world default is passed to chunk optimization.</param>
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
        /// <summary>Gets or creates the chunk containing a world-space point.</summary>
        public IChunk2D<T> GetChunkAt(int worldX, int worldY)
        {
            (int chunkX, int chunkY, _, _) = GetChunkCoords(worldX, worldY);
            return GetChunk(chunkX, chunkY);
        }

        private (int chunkX, int chunkY, int localX, int localY) GetChunkCoords(int worldX, int worldY)
        {
            int chunkX = worldX / chunkSize;
            int chunkY = worldY / chunkSize;
            int localX = worldX % chunkSize;
            int localY = worldY % chunkSize;

            if (localX < 0) { chunkX -= 1; localX += chunkSize; }
            if (localY < 0) { chunkY -= 1; localY += chunkSize; }
            return (chunkX, chunkY, localX, localY);
        }

        /// <summary>Reads or writes a world-space cell, creating its chunk when needed.</summary>
        public T this[int worldX, int worldY]
        {
            get => GetTile(worldX, worldY);
            set => SetTile(worldX, worldY, value);
        }

        /// <summary>Reads a cell, creating its chunk unless the point is outside enabled bounds.</summary>
        public T GetTile(int worldX, int worldY)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY))) return defaultValue;
            (int chunkX, int chunkY, int localX, int localY) = GetChunkCoords(worldX, worldY);
            return GetChunk(chunkX, chunkY)[localX, localY];
        }

        /// <summary>Writes a cell, creating its chunk unless the point is outside enabled bounds.</summary>
        public void SetTile(int worldX, int worldY, T value)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY))) return;
            (int chunkX, int chunkY, int localX, int localY) = GetChunkCoords(worldX, worldY);
            GetChunk(chunkX, chunkY)[localX, localY] = value;
        }

        /// <summary>Reads only an existing in-bounds chunk; returns false and the supplied fallback otherwise.</summary>
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

        /// <summary>Writes only to an existing in-bounds chunk; returns false if none exists.</summary>
        public bool TrySetTile(int worldX, int worldY, T value)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY))) return false;
            (int chunkX, int chunkY, int localX, int localY) = GetChunkCoords(worldX, worldY);
            if (!chunks.TryGetValue((chunkX, chunkY), out var chunk))
                return false;

            chunk[localX, localY] = value;
            return true;
        }

        /// <summary>Removes an existing chunk by chunk coordinates without creating one.</summary>
        public void RemoveSchunk(int chunkX, int chunkY)
        {
            chunks.Remove((chunkX, chunkY));
        }

        /// <summary>Optimizes an existing chunk against the world default and removes it when optimization returns null.</summary>
        public void OptimizeChunk(int chunkX, int chunkY)
        {
            var key = (chunkX, chunkY);
            if (!chunks.TryGetValue(key, out var chunk)) return;
            IChunk2D<T>? optimized;
            if (chunk is DenseChunk2D<K, T> denseChunk)
                optimized = denseChunk.Optimize(defaultValue);
            else if (chunk is SparseChunk2D<K, T> sparseChunk)
                optimized = sparseChunk.Optimize(defaultValue);
            else return;

            if (optimized == null) chunks.Remove(key);
            else if (!ReferenceEquals(optimized, chunk)) chunks[key] = optimized;
        }

        /// <summary>Applies OptimizeChunk to a snapshot of all loaded chunk keys.</summary>
        public void OptimizeAllChunks()
        {
            var keys = new List<(int, int)>(chunks.Keys);
            foreach (var key in keys) OptimizeChunk(key.Item1, key.Item2);
        }

        /// <summary>Creates a serializer with optional value replacements.</summary>
        public ChunkSerializer2D<K, T> GetSerializer(Dictionary<T, T>? replacements)
        {
            return new ChunkSerializer2D<K, T>(replacements);
        }

        /// <summary>Creates a direct serializer for concrete dense or sparse chunks without replacements.</summary>
        public ChunkBlitSerializer2D<K, T> GetFastSerializer()
        {
            return new ChunkBlitSerializer2D<K, T>();
        }
    }
}
