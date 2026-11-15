using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    /// <summary>A lazily created grid of 3D chunks addressed by world coordinates.</summary>
    /// <remarks>The chunk map supports concurrent access, but callers must coordinate simultaneous access to each chunk's cells and mutable world settings.</remarks>
    /// <example><![CDATA[
    /// var world = new World3D<sbyte, string>(_ => new SparseChunk3D<sbyte, string>(16, "empty"));
    /// world[-1, 0, 2] = "stone";
    /// world.OptimizeAllChunks();
    /// foreach (var entry in world.EnumerateChunks())
    /// {
    ///     IChunk3D<string> chunk = entry.Value;
    /// }
    /// ]]></example>
    public class World3D<K, T> where K : unmanaged
    {
        /// <summary>Value returned outside bounds and passed to chunk optimization.</summary>
        public T defaultValue;

        private readonly ConcurrentDictionary<(int, int, int), IChunk3D<T>> chunks = new();

        /// <summary>Creates a chunk for the supplied inclusive world-space range.</summary>
        /// <remarks>Concurrent first access can call the factory more than once for the same coordinate; only one result is stored.</remarks>
        public delegate IChunk3D<T> ChunkFactoryDelegate(Range3D range);
        private readonly ChunkFactoryDelegate chunkFactory;
        private readonly int chunkSize;
        /// <summary>Side length of each cubic chunk.</summary>
        public int ChunkSize => chunkSize;

        /// <summary>Enumerates chunks already created by this world and their chunk-coordinate keys. Concurrent changes may appear during enumeration.</summary>
        public IEnumerable<KeyValuePair<(int x, int y, int z), IChunk3D<T>>> EnumerateChunks()
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
        public World3D(ChunkFactoryDelegate factory, int chunkSize = 16)
        {
            this.chunkFactory = factory;
            this.chunkSize = chunkSize;
            defaultValue = default!;
        }

        /// <summary>Creates a world with an explicit default value, including null or default(T).</summary>
        public World3D(ChunkFactoryDelegate factory, int chunkSize, T defaultValue)
        {
            this.chunkFactory = factory;
            this.chunkSize = chunkSize;
            this.defaultValue = defaultValue;
        }

        private IChunk3D<T> GetChunk(int chunkX, int chunkY, int chunkZ)
        {
            return chunks.GetOrAdd((chunkX, chunkY, chunkZ), key => chunkFactory(new Range3D(
                key.Item1 * chunkSize, (key.Item1 + 1) * chunkSize - 1,
                key.Item2 * chunkSize, (key.Item2 + 1) * chunkSize - 1,
                key.Item3 * chunkSize, (key.Item3 + 1) * chunkSize - 1)));
        }

        /// <summary>Gets or creates the chunk containing a world-space point.</summary>
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
            chunks.TryRemove((chunkX, chunkY, chunkZ), out _);
        }

        /// <summary>Optimizes an existing chunk against the world default and removes it when optimization returns null.</summary>
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

            if (optimized == null)
                ((ICollection<KeyValuePair<(int, int, int), IChunk3D<T>>>)chunks).Remove(new(key, chunk));
            else if (!ReferenceEquals(optimized, chunk))
                chunks.TryUpdate(key, optimized, chunk);
        }

        /// <summary>Applies OptimizeChunk to a snapshot of all loaded chunk keys.</summary>
        public void OptimizeAllChunks()
        {
            var keys = new List<(int, int, int)>(chunks.Keys);
            foreach (var key in keys) OptimizeChunk(key.Item1, key.Item2, key.Item3);
        }

        /// <summary>Optimizes a snapshot of loaded chunk keys in parallel. Callers must coordinate access to each chunk and changes to world settings while it is being optimized.</summary>
        public void OptimizeAllChunksParallel()
        {
            Parallel.ForEach(chunks.Keys, key => OptimizeChunk(key.Item1, key.Item2, key.Item3));
        }

    }
}
