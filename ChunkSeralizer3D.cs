using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    public class ChunkSerializer3D<K, T> where K : unmanaged where T : unmanaged
    {
        private readonly Dictionary<T, T>? replacementTable;
        private readonly ChunkBlitSerializer3D<K, T> blit = new();

        public ChunkSerializer3D() { }

        public ChunkSerializer3D(Dictionary<T, T>? replacement)
        {
            replacementTable = replacement;
        }

        private T Replace(T value)
        {
            return replacementTable != null && replacementTable.TryGetValue(value, out T replaced)
                ? replaced : value;
        }

        public unsafe byte[] SerializeDense(IChunk3D<T> chunk)
        {
            if (chunk is DenseChunk3D<K, T> emptyDense && emptyDense.tiles == null)
                return blit.SerializeEmptyDense(Replace(chunk.DefaultValue));
            if (replacementTable == null && chunk is DenseChunk3D<K, T> dense)
                return blit.SerializeDense(dense);

            int cells = checked(chunk.Size * chunk.Size * chunk.Size);
            byte[] data = new byte[checked(1 + sizeof(T) + cells * sizeof(T))];
            fixed (byte* target = data)
            {
                target[0] = 0;
                T defaultValue = Replace(chunk.DefaultValue);
                Buffer.MemoryCopy(&defaultValue, target + 1, sizeof(T), sizeof(T));
                byte* current = target + 1 + sizeof(T);
                for (int z = 0; z < chunk.Size; z++)
                    for (int y = 0; y < chunk.Size; y++)
                        for (int x = 0; x < chunk.Size; x++)
                        {
                            T value = Replace(chunk[x, y, z]);
                            Buffer.MemoryCopy(&value, current, sizeof(T), sizeof(T));
                            current += sizeof(T);
                        }
            }
            return data;
        }

        public unsafe byte[] SerializeSparse(IChunk3D<T> chunk, bool packedXYZ = false)
        {
            if (packedXYZ && chunk.Size != 16)
                throw new ArgumentException("Packed XYZ requires a chunk size of 16", nameof(chunk));
            if (chunk is DenseChunk3D<K, T> emptyDense && emptyDense.tiles == null)
                return blit.SerializeEmptyDense(Replace(chunk.DefaultValue));
            if (replacementTable == null && chunk is SparseChunk3D<K, T> sparse)
                return blit.SerializeSparse(sparse, packedXYZ);

            var converted = new SparseChunk3D<K, T>(chunk.Size, Replace(chunk.DefaultValue));
            foreach (var (x, y, z, value) in chunk.Enumerate())
                converted[x, y, z] = Replace(value);
            return blit.SerializeSparse(converted, packedXYZ);
        }

        // [format][default value][size-dependent RLE runs]
        public unsafe byte[] SerializeRLE(IChunk3D<T> chunk)
        {
            if (chunk is DenseChunk3D<K, T> emptyDense && emptyDense.tiles == null)
                return blit.SerializeEmptyDense(Replace(chunk.DefaultValue));
            int cells = checked(chunk.Size * chunk.Size * chunk.Size);
            return RLEEncoder.Serialize(EnumerateValues(chunk), Replace(chunk.DefaultValue), cells);
        }

        private IEnumerable<T> EnumerateValues(IChunk3D<T> chunk)
        {
            for (int z = 0; z < chunk.Size; z++)
                for (int y = 0; y < chunk.Size; y++)
                    for (int x = 0; x < chunk.Size; x++)
                        yield return Replace(chunk[x, y, z]);
        }

        public unsafe byte[] SerializeOptimal(IChunk3D<T> chunk, bool exact)
        {
            if (!exact)
            {
                T defaultValue = Replace(chunk.DefaultValue);
                int stored = 0;
                if (replacementTable == null && chunk is SparseChunk3D<K, T> knownSparse)
                    stored = knownSparse.tiles.Count;
                else
                    foreach (var (_, _, _, value) in chunk.Enumerate())
                        if (!Replace(value).Equals(defaultValue)) stored++;

                long denseBytes = (long)chunk.Size * chunk.Size * chunk.Size * sizeof(T);
                bool packedXYZ = chunk.Size == 16;
                long sparseBytes = (long)stored * ((packedXYZ ? sizeof(ushort) : 3 * sizeof(K)) + sizeof(T));
                return sparseBytes < denseBytes ? SerializeSparse(chunk, packedXYZ) : SerializeDense(chunk);
            }

            byte[] dense = SerializeDense(chunk);
            byte[] sparse = SerializeSparse(chunk, chunk.Size == 16);
            byte[] rle = SerializeRLE(chunk);
            if (rle.Length < dense.Length && rle.Length < sparse.Length) return rle;
            return sparse.Length < dense.Length ? sparse : dense;
        }
    }
}
