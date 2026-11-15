using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PiDev.VoxelCore
{
    /// <summary>A lazily created grid of 2D chunks addressed by world coordinates.</summary>
    /// <remarks>The chunk map supports concurrent access, but callers must coordinate simultaneous access to each chunk's cells and mutable world settings.</remarks>
    /// <example><![CDATA[
    /// var world = new World2DFast<byte, int>(_ => new SparseChunk2DFast<byte, int>(16, 0));
    /// world[-1, 3] = 7;
    /// foreach (var entry in world.EnumerateChunks())
    /// {
    ///     byte[] data = world.GetSerializer(null).SerializeOptimal(entry.Value, true);
    /// }
    /// ]]></example>
    public class World2DFast<K, T> where T : unmanaged where K : unmanaged
    {
        /// <summary>Records whether a default value was supplied to the constructor.</summary>
        public bool usePreferredDefaultValue;
        /// <summary>Value returned outside bounds and passed to chunk optimization.</summary>
        public T defaultValue;

        private readonly ConcurrentDictionary<(int, int), IChunk2DFast<T>> chunks = new();

        /// <summary>Creates a chunk for the supplied inclusive world-space range.</summary>
        /// <remarks>Concurrent first access can call the factory more than once for the same coordinate; only one result is stored.</remarks>
        public delegate IChunk2DFast<T> ChunkFactoryDelegate(Range2D range);
        private readonly ChunkFactoryDelegate chunkFactory;
        private readonly int chunkSize;
        /// <summary>Side length of each square chunk.</summary>
        public int ChunkSize => chunkSize;

        /// <summary>Enumerates chunks already created by this world and their chunk-coordinate keys. Concurrent changes may appear during enumeration.</summary>
        public IEnumerable<KeyValuePair<(int x, int y), IChunk2DFast<T>>> EnumerateChunks()
        {
            foreach (var kv in chunks) yield return kv;
        }

        /// <summary>Optional inclusive bounds for tile reads and writes; null disables bounds checking.</summary>
        public Range2D? worldBounds;

        /// <summary>Creates a world that produces chunks on first access.</summary>
        /// <param name="factory">Receives the new chunk's inclusive world-space range.</param>
        /// <param name="chunkSize">Side length used for coordinate mapping.</param>
        /// <param name="defaultValue">Optional world default; when omitted, default(T) is used. The stored world default is passed to chunk optimization.</param>
        public World2DFast(ChunkFactoryDelegate factory, int chunkSize = 16, T? defaultValue = null)
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

        private IChunk2DFast<T> GetChunk(int chunkX, int chunkY)
        {
            return chunks.GetOrAdd((chunkX, chunkY), key => chunkFactory(new Range2D(
                key.Item1 * chunkSize, (key.Item1 + 1) * chunkSize - 1,
                key.Item2 * chunkSize, (key.Item2 + 1) * chunkSize - 1)));
        }
        /// <summary>Gets or creates the chunk containing a world-space point.</summary>
        public IChunk2DFast<T> GetChunkAt(int worldX, int worldY)
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
            if (worldBounds is Range2D bounds && !bounds.Contains((worldX, worldY))) return defaultValue;
            (int chunkX, int chunkY, int localX, int localY) = GetChunkCoords(worldX, worldY);
            return GetChunk(chunkX, chunkY)[localX, localY];
        }

        /// <summary>Writes a cell, creating its chunk unless the point is outside enabled bounds.</summary>
        public void SetTile(int worldX, int worldY, T value)
        {
            if (worldBounds is Range2D bounds && !bounds.Contains((worldX, worldY))) return;
            (int chunkX, int chunkY, int localX, int localY) = GetChunkCoords(worldX, worldY);
            GetChunk(chunkX, chunkY)[localX, localY] = value;
        }

        /// <summary>Reads only an existing in-bounds chunk; returns false and the supplied fallback otherwise.</summary>
        public bool TryGetTile(int worldX, int worldY, out T value, T defaultValue)
        {
            if (worldBounds is Range2D bounds && !bounds.Contains((worldX, worldY))) { value = defaultValue; return false; }
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
            if (worldBounds is Range2D bounds && !bounds.Contains((worldX, worldY))) return false;
            (int chunkX, int chunkY, int localX, int localY) = GetChunkCoords(worldX, worldY);
            if (!chunks.TryGetValue((chunkX, chunkY), out var chunk))
                return false;

            chunk[localX, localY] = value;
            return true;
        }

        /// <summary>Inserts or replaces a chunk at chunk coordinates, or removes it when chunk is null.</summary>
        /// <remarks>Callers must coordinate access to an existing chunk while replacing it.</remarks>
        public void SetChunk(int chunkX, int chunkY, IChunk2DFast<T>? chunk)
        {
            if (chunk == null)
            {
                chunks.TryRemove((chunkX, chunkY), out _);
                return;
            }
            if (chunk.Size != chunkSize) throw new ArgumentException("Chunk size must match the world chunk size.", nameof(chunk));
            chunks[(chunkX, chunkY)] = chunk;
        }

        /// <summary>Removes an existing chunk by chunk coordinates without creating one.</summary>
        public void RemoveChunk(int chunkX, int chunkY)
        {
            chunks.TryRemove((chunkX, chunkY), out _);
        }

        /// <summary>Optimizes an existing chunk against the world default and removes it when optimization returns null.</summary>
        public void OptimizeChunk(int chunkX, int chunkY)
        {
            var key = (chunkX, chunkY);
            if (!chunks.TryGetValue(key, out var chunk)) return;
            IChunk2DFast<T>? optimized;
            if (chunk is DenseChunk2DFast<K, T> denseChunk)
                optimized = denseChunk.Optimize(defaultValue);
            else if (chunk is SparseChunk2DFast<K, T> sparseChunk)
                optimized = sparseChunk.Optimize(defaultValue);
            else return;

            if (optimized == null)
                ((ICollection<KeyValuePair<(int, int), IChunk2DFast<T>>>)chunks).Remove(new(key, chunk));
            else if (!ReferenceEquals(optimized, chunk))
                chunks.TryUpdate(key, optimized, chunk);
        }

        /// <summary>Applies OptimizeChunk to a snapshot of all loaded chunk keys.</summary>
        public void OptimizeAllChunks()
        {
            var keys = new List<(int, int)>(chunks.Keys);
            foreach (var key in keys) OptimizeChunk(key.Item1, key.Item2);
        }

        /// <summary>Optimizes a snapshot of loaded chunk keys in parallel. Callers must coordinate access to each chunk and changes to world settings while it is being optimized.</summary>
        public void OptimizeAllChunksParallel()
        {
            Parallel.ForEach(chunks.Keys, key => OptimizeChunk(key.Item1, key.Item2));
        }

        /// <summary>Creates a serializer with optional value replacements.</summary>
        public ChunkSerializer2D<K, T> GetSerializer(Dictionary<T, T>? replacements = null)
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
