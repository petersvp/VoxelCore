using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    public interface IChunk3D<T> where T : struct
    {
        T this[int x, int y, int z] { get; set; }
    }

    public class SparseChunk3D<T> : IChunk3D<T> where T : struct
    {
        private Dictionary<(int, int, int), T> tiles = new();
        public T defaultValue;

        public SparseChunk3D(T defaultValue)
        {
            this.defaultValue = defaultValue;
        }

        public T this[int x, int y, int z]
        {
            get => tiles.TryGetValue((x, y, z), out T value) ? value : defaultValue;
            set
            {
                if (value.Equals(defaultValue))
                    tiles.Remove((x, y, z)); // Remove redundant default values
                else
                    tiles[(x, y, z)] = value;
            }
        }

        public void Optimize()
        {
            if (tiles.Count == 0) return;

            var frequency = new Dictionary<T, int>();
            T mostCommonValue = defaultValue;
            int mostCommonCount = 0;

            foreach (var value in tiles.Values)
            {
                if (!frequency.ContainsKey(value))
                    frequency[value] = 0;

                frequency[value]++;

                if (frequency[value] > mostCommonCount)
                {
                    mostCommonCount = frequency[value];
                    mostCommonValue = value;
                }
            }

            if (!mostCommonValue.Equals(defaultValue))
            {
                defaultValue = mostCommonValue;

                var newTiles = new Dictionary<(int, int, int), T>();
                foreach (var kvp in tiles)
                {
                    if (!kvp.Value.Equals(defaultValue))
                        newTiles[kvp.Key] = kvp.Value;
                }

                tiles = newTiles;
            }
        }

        public DenseChunk3D<T> ToDenseChunk(int size = 16)
        {
            var denseChunk = new DenseChunk3D<T>(size, defaultValue);
            foreach (var kvp in tiles) denseChunk[kvp.Key.Item1, kvp.Key.Item2, kvp.Key.Item3] = kvp.Value;
            return denseChunk;
        }
    }

    public class DenseChunk3D<T> : IChunk3D<T> where T : struct
    {
        private readonly T[,,] tiles;
        private readonly int size;
        private readonly T defaultValue;

        public DenseChunk3D(int size = 16, T defaultValue = default)
        {
            this.size = size;
            this.defaultValue = defaultValue;
            tiles = new T[size, size, size];

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

        public SparseChunk3D<T> ToSparseChunk()
        {
            var sparseChunk = new SparseChunk3D<T>(defaultValue);
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    for (int z = 0; z < size; z++)
                    {
                        var value = tiles[x, y, z];
                        if (!value.Equals(defaultValue))
                            sparseChunk[x, y, z] = value;
                    }
            return sparseChunk;
        }
    }

    public class World3D<T> where T : struct
    {
        private readonly Dictionary<(int, int, int), IChunk3D<T>> chunks = new();
        private readonly Func<IChunk3D<T>> chunkFactory;
        private readonly int chunkSize;

        public World3D(Func<IChunk3D<T>> factory, int chunkSize = 16)
        {
            this.chunkFactory = factory;
            this.chunkSize = chunkSize;
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
            if (!chunks.TryGetValue((chunkX, chunkY, chunkZ), out var chunk))
                return false;

            chunk[localX, localY, localZ] = value;
            return true;
        }

        public void UnloadChunk(int chunkX, int chunkY, int chunkZ)
        {
            chunks.Remove((chunkX, chunkY, chunkZ));
        }
    }
}
