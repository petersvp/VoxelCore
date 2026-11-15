using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>A lazily created grid of 3D chunks addressed by world coordinates.</summary>
    /// <example><![CDATA[
    /// var world = new World3D<sbyte, bool>(_ => new SparseChunk3D<sbyte, bool>(16, false));
    /// world[-1, 0, 2] = true;
    /// world.OptimizeAllChunks();
    /// foreach (var entry in world.EnumerateChunks())
    /// {
    ///     byte[] data = world.GetSerializer(null).SerializeOptimal(entry.Value, true);
    /// }
    /// ]]></example>
    public class World3DFast<K, T> where T : unmanaged where K : unmanaged
    {
        /// <summary>Value returned outside bounds and passed to chunk optimization.</summary>
        public T defaultValue;

        private readonly Dictionary<(int, int, int), IChunk3DFast<T>> chunks = new();

        /// <summary>Creates a chunk for the supplied inclusive world-space range.</summary>
        public delegate IChunk3DFast<T> ChunkFactoryDelegate(Range3D range);
        private readonly ChunkFactoryDelegate chunkFactory;
        private readonly int chunkSize;
        /// <summary>Side length of each cubic chunk.</summary>
        public int ChunkSize => chunkSize;

        /// <summary>Enumerates chunks already created by this world and their chunk-coordinate keys.</summary>
        public IEnumerable<KeyValuePair<(int x, int y, int z), IChunk3DFast<T>>> EnumerateChunks()
        {
            foreach (var kv in chunks) yield return kv;
        }

        /// <summary>Whether tile reads and writes are limited to worldBounds.</summary>
        public bool useBoundChecking;
        /// <summary>Inclusive bounds applied when useBoundChecking is true.</summary>
        public Range3D worldBounds;

        /// <summary>Creates a world that produces chunks on first access.</summary>
        /// <param name="factory">Receives the new chunk's inclusive world-space range.</param>
        /// <param name="chunkSize">Side length used for coordinate mapping.</param>
        public World3DFast(ChunkFactoryDelegate factory, int chunkSize = 16)
        {
            this.chunkFactory = factory;
            this.chunkSize = chunkSize;
        }

        private IChunk3DFast<T> GetChunk(int chunkX, int chunkY, int chunkZ)
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

        /// <summary>Gets or creates the chunk containing a world-space point.</summary>
        public IChunk3DFast<T> GetChunkAt(int worldX, int worldY, int worldZ)
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

        /// <summary>Reads or writes a world-space cell, creating its chunk when needed.</summary>
        public T this[int worldX, int worldY, int worldZ]
        {
            get => GetTile(worldX, worldY, worldZ);
            set => SetTile(worldX, worldY, worldZ, value);
        }

        /// <summary>Reads a cell, creating its chunk unless the point is outside enabled bounds.</summary>
        public T GetTile(int worldX, int worldY, int worldZ)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY, worldZ))) return defaultValue;
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            return GetChunk(chunkX, chunkY, chunkZ)[localX, localY, localZ];
        }

        /// <summary>Writes a cell, creating its chunk unless the point is outside enabled bounds.</summary>
        public void SetTile(int worldX, int worldY, int worldZ, T value)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY, worldZ))) return;
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            GetChunk(chunkX, chunkY, chunkZ)[localX, localY, localZ] = value;
        }

        /// <summary>Reads only an existing in-bounds chunk; returns false and the supplied fallback otherwise.</summary>
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

        /// <summary>Writes only to an existing in-bounds chunk; returns false if none exists.</summary>
        public bool TrySetTile(int worldX, int worldY, int worldZ, T value)
        {
            if (useBoundChecking && !worldBounds.Contains((worldX, worldY, worldZ))) return false;
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            if (!chunks.TryGetValue((chunkX, chunkY, chunkZ), out var chunk)) return false;
            chunk[localX, localY, localZ] = value;
            return true;
        }

        /// <summary>Removes an existing chunk by chunk coordinates without creating one.</summary>
        public void RemoveSchunk(int chunkX, int chunkY, int chunkZ)
        {
            chunks.Remove((chunkX, chunkY, chunkZ));
        }

        /// <summary>Optimizes an existing chunk against the world default and removes it when optimization returns null.</summary>
        public void OptimizeChunk(int chunkX, int chunkY, int chunkZ)
        {
            var key = (chunkX, chunkY, chunkZ);
            if (!chunks.TryGetValue(key, out var chunk)) return;
            IChunk3DFast<T>? optimized;
            if (chunk is DenseChunk3DFast<K, T> denseChunk)
                optimized = denseChunk.Optimize(defaultValue);
            else if (chunk is SparseChunk3DFast<K, T> sparseChunk)
                optimized = sparseChunk.Optimize(defaultValue);
            else return;

            if (optimized == null) chunks.Remove(key);
            else if (!ReferenceEquals(optimized, chunk)) chunks[key] = optimized;
        }

        /// <summary>Applies OptimizeChunk to a snapshot of all loaded chunk keys.</summary>
        public void OptimizeAllChunks()
        {
            var keys = new List<(int, int, int)>(chunks.Keys);
            foreach (var key in keys) OptimizeChunk(key.Item1, key.Item2, key.Item3);
        }

        /// <summary>Creates a serializer with optional value replacements.</summary>
        public ChunkSerializer3D<K, T> GetSerializer(Dictionary<T, T>? replacements = null)
        {
            return new ChunkSerializer3D<K, T>(replacements);
        }

        /// <summary>Creates a direct serializer for concrete dense or sparse chunks without replacements.</summary>
        public ChunkBlitSerializer3D<K, T> GetFastSerializer()
        {
            return new ChunkBlitSerializer3D<K, T>();
        }
    }
}
