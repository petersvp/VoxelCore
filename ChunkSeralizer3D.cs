using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    public unsafe class ChunkSerializer3D<K, T> where K : unmanaged where T : unmanaged
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

        public byte[] SerializeDense(IChunk3D<T> chunk)
        {
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

        public byte[] SerializeSparse(IChunk3D<T> chunk)
        {
            if (replacementTable == null && chunk is SparseChunk3D<K, T> sparse)
                return blit.SerializeSparse(sparse);

            var converted = new SparseChunk3D<K, T>(chunk.Size, Replace(chunk.DefaultValue));
            foreach (var (x, y, z, value) in chunk.Enumerate())
                converted[x, y, z] = Replace(value);
            return blit.SerializeSparse(converted);
        }

        // [format][default value]{[run length][value]}
        public byte[] SerializeRLE(IChunk3D<T> chunk)
        {
            int cells = checked(chunk.Size * chunk.Size * chunk.Size);
            int runs = 0;
            T previous = default;
            bool first = true;
            for (int z = 0; z < chunk.Size; z++)
                for (int y = 0; y < chunk.Size; y++)
                    for (int x = 0; x < chunk.Size; x++)
                    {
                        T value = Replace(chunk[x, y, z]);
                        if (first || !value.Equals(previous)) { runs++; previous = value; first = false; }
                    }

            int entrySize = sizeof(int) + sizeof(T);
            byte[] data = new byte[checked(1 + sizeof(T) + runs * entrySize)];
            fixed (byte* target = data)
            {
                target[0] = 3;
                T defaultValue = Replace(chunk.DefaultValue);
                Buffer.MemoryCopy(&defaultValue, target + 1, sizeof(T), sizeof(T));
                int offset = 1 + sizeof(T);
                previous = default;
                first = true;
                int count = 0;
                for (int z = 0; z < chunk.Size; z++)
                    for (int y = 0; y < chunk.Size; y++)
                        for (int x = 0; x < chunk.Size; x++)
                        {
                            T value = Replace(chunk[x, y, z]);
                            if (!first && value.Equals(previous)) { count++; continue; }
                            if (!first)
                            {
                                ChunkBlitSerializer3D<K, T>.WriteInt32(target + offset, count);
                                Buffer.MemoryCopy(&previous, target + offset + 4, sizeof(T), sizeof(T));
                                offset += entrySize;
                            }
                            previous = value;
                            count = 1;
                            first = false;
                        }
                if (cells > 0)
                {
                    ChunkBlitSerializer3D<K, T>.WriteInt32(target + offset, count);
                    Buffer.MemoryCopy(&previous, target + offset + 4, sizeof(T), sizeof(T));
                }
            }
            return data;
        }

        public byte[] SerializeOptimal(IChunk3D<T> chunk, bool exact)
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
                long sparseBytes = (long)stored * (3 * sizeof(K) + sizeof(T));
                return sparseBytes < denseBytes ? SerializeSparse(chunk) : SerializeDense(chunk);
            }

            byte[] dense = SerializeDense(chunk);
            byte[] sparse = SerializeSparse(chunk);
            byte[] rle = SerializeRLE(chunk);
            if (rle.Length < dense.Length && rle.Length < sparse.Length) return rle;
            return sparse.Length < dense.Length ? sparse : dense;
        }
    }
}
