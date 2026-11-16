using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    /// <summary>A lazily created grid of 3D chunks addressed by world coordinates.</summary>
    /// <remarks>The chunk map supports concurrent access, but callers must coordinate simultaneous access to each chunk's cells and mutable world settings.</remarks>
    /// <example><![CDATA[
    /// var world = new World3DFast<sbyte, bool>(_ => new SparseChunk3DFast<sbyte, bool>(16, false));
    /// world[-1, 0, 2] = true;
    /// world.OptimizeAllChunks();
    /// foreach (var entry in world.EnumerateChunks())
    /// {
    ///     byte[] data = world.GetSerializer(null).SerializeOptimal(entry.Value, true);
    /// }
    /// ]]></example>
    public class World3DFast<K, T> : IVoxelDataAccess3D<T> where T : unmanaged where K : unmanaged
    {
        /// <summary>Value returned outside bounds and passed to chunk optimization.</summary>
        public T defaultValue;

        private readonly ConcurrentDictionary<(int, int, int), IChunk3DFast<T>> chunks = new();

        /// <summary>Creates a chunk for the supplied inclusive world-space range.</summary>
        /// <remarks>Concurrent first access can call the factory more than once for the same coordinate; only one result is stored.</remarks>
        public delegate IChunk3DFast<T> ChunkFactoryDelegate(Range3D range);
        private readonly ChunkFactoryDelegate chunkFactory;
        private readonly int chunkSize;
        /// <summary>Side length of each cubic chunk.</summary>
        public int ChunkSize => chunkSize;

        /// <summary>Enumerates chunks already created by this world and their chunk-coordinate keys. Concurrent changes may appear during enumeration.</summary>
        public IEnumerable<KeyValuePair<(int x, int y, int z), IChunk3DFast<T>>> EnumerateChunks()
        {
            foreach (var kv in chunks) yield return kv;
        }

        /// <summary>Yields independent world-coordinate sequences from loaded chunks intersecting the inclusive range.</summary>
        /// <remarks>Exact filtering runs inside each sequence; non-exact sequences may include cells outside range. Missing chunks are not created.</remarks>
        public IEnumerable<IEnumerable<(int x, int y, int z, T value)>> EnumerateRange(Range3D range, bool exact)
        {
            if (range.minX > range.maxX || range.minY > range.maxY || range.minZ > range.maxZ) yield break;
            var min = GetChunkCoords(range.minX, range.minY, range.minZ);
            var max = GetChunkCoords(range.maxX, range.maxY, range.maxZ);
            foreach (var entry in chunks)
            {
                var key = entry.Key;
                if (key.Item1 < min.chunkX || key.Item1 > max.chunkX ||
                    key.Item2 < min.chunkY || key.Item2 > max.chunkY ||
                    key.Item3 < min.chunkZ || key.Item3 > max.chunkZ) continue;
                int x = key.Item1 * chunkSize, y = key.Item2 * chunkSize, z = key.Item3 * chunkSize;
                yield return exact ? EnumerateChunkWorldExact(entry.Value, x, y, z, range)
                    : EnumerateChunkWorld(entry.Value, x, y, z);
            }
        }

        private static IEnumerable<(int x, int y, int z, T value)> EnumerateChunkWorld(
            IChunk3DFast<T> chunk, int offsetX, int offsetY, int offsetZ)
        {
            foreach (var cell in chunk.Enumerate())
                yield return (offsetX + cell.x, offsetY + cell.y, offsetZ + cell.z, cell.value);
        }

        private static IEnumerable<(int x, int y, int z, T value)> EnumerateChunkWorldExact(
            IChunk3DFast<T> chunk, int offsetX, int offsetY, int offsetZ, Range3D range)
        {
            foreach (var cell in chunk.Enumerate())
            {
                int x = offsetX + cell.x, y = offsetY + cell.y, z = offsetZ + cell.z;
                if (range.Contains(x, y, z)) yield return (x, y, z, cell.value);
            }
        }

        /// <summary>Optional inclusive bounds for tile reads and writes; null disables bounds checking.</summary>
        public Range3D? worldBounds;

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
            return chunks.GetOrAdd((chunkX, chunkY, chunkZ), key => chunkFactory(new Range3D(
                key.Item1 * chunkSize, (key.Item1 + 1) * chunkSize - 1,
                key.Item2 * chunkSize, (key.Item2 + 1) * chunkSize - 1,
                key.Item3 * chunkSize, (key.Item3 + 1) * chunkSize - 1)));
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
            if (worldBounds is Range3D bounds && !bounds.Contains((worldX, worldY, worldZ))) return defaultValue;
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            return GetChunk(chunkX, chunkY, chunkZ)[localX, localY, localZ];
        }

        /// <summary>Writes a cell, creating its chunk unless the point is outside enabled bounds.</summary>
        public void SetTile(int worldX, int worldY, int worldZ, T value)
        {
            if (worldBounds is Range3D bounds && !bounds.Contains((worldX, worldY, worldZ))) return;
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            GetChunk(chunkX, chunkY, chunkZ)[localX, localY, localZ] = value;
        }

        /// <summary>Reads only an existing in-bounds chunk; returns false and the supplied fallback otherwise.</summary>
        public bool TryGetTile(int worldX, int worldY, int worldZ, out T value, T defaultValue)
        {
            if (worldBounds is Range3D bounds && !bounds.Contains((worldX, worldY, worldZ))) { value = defaultValue; return false; }
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
            if (worldBounds is Range3D bounds && !bounds.Contains((worldX, worldY, worldZ))) return false;
            (int chunkX, int chunkY, int chunkZ, int localX, int localY, int localZ) = GetChunkCoords(worldX, worldY, worldZ);
            if (!chunks.TryGetValue((chunkX, chunkY, chunkZ), out var chunk)) return false;
            chunk[localX, localY, localZ] = value;
            return true;
        }

        /// <summary>Inserts or replaces a chunk at chunk coordinates, or removes it when chunk is null.</summary>
        /// <remarks>Callers must coordinate access to an existing chunk while replacing it.</remarks>
        public void SetChunk(int chunkX, int chunkY, int chunkZ, IChunk3DFast<T>? chunk)
        {
            if (chunk == null)
            {
                chunks.TryRemove((chunkX, chunkY, chunkZ), out _);
                return;
            }
            if (chunk.Size != chunkSize) throw new ArgumentException("Chunk size must match the world chunk size.", nameof(chunk));
            chunks[(chunkX, chunkY, chunkZ)] = chunk;
        }

        /// <summary>Removes an existing chunk by chunk coordinates without creating one.</summary>
        public void RemoveChunk(int chunkX, int chunkY, int chunkZ)
        {
            chunks.TryRemove((chunkX, chunkY, chunkZ), out _);
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

            if (optimized == null)
                ((ICollection<KeyValuePair<(int, int, int), IChunk3DFast<T>>>)chunks).Remove(new(key, chunk));
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
