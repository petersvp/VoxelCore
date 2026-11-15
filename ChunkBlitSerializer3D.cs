using System;

namespace PiDev.VoxelCore
{
    public unsafe class ChunkBlitSerializer3D<K, T> where K : unmanaged where T : unmanaged
    {
        // [format][default value][cell data with x varying fastest]
        public byte[] SerializeDense(DenseChunk3D<K, T> chunk)
        {
            if (chunk.tiles == null) return SerializeEmptyDense(chunk.DefaultValue);

            int cells = checked(chunk.Size * chunk.Size * chunk.Size);
            int bytes = checked(cells * sizeof(T));
            byte[] data = new byte[checked(1 + sizeof(T) + bytes)];
            fixed (byte* target = data)
            {
                target[0] = 0;
                T defaultValue = chunk.DefaultValue;
                Buffer.MemoryCopy(&defaultValue, target + 1, sizeof(T), sizeof(T));
                byte* tileData = target + 1 + sizeof(T);
                Buffer.MemoryCopy(chunk.tiles, tileData, bytes, bytes);
            }
            return data;
        }

        internal byte[] SerializeEmptyDense(T defaultValue)
        {
            byte[] data = new byte[1 + sizeof(T)];
            fixed (byte* target = data)
            {
                target[0] = 4;
                Buffer.MemoryCopy(&defaultValue, target + 1, sizeof(T), sizeof(T));
            }
            return data;
        }

        // Format 1: raw K x/y/z. Format 2: little-endian [xxxx yyyy zzzz 0000].
        public byte[] SerializeSparse(SparseChunk3D<K, T> chunk, bool packedXYZ = false)
        {
            if (packedXYZ && chunk.Size != 16)
                throw new ArgumentException("Packed XYZ requires a chunk size of 16", nameof(chunk));
            int keySize = sizeof(K);
            int coordinateSize = packedXYZ ? sizeof(ushort) : 3 * keySize;
            int entrySize = checked(coordinateSize + sizeof(T));
            byte[] data = new byte[checked(1 + sizeof(T) + chunk.tiles.Count * entrySize)];
            fixed (byte* target = data)
            {
                target[0] = (byte)(packedXYZ ? 2 : 1);
                T defaultValue = chunk.DefaultValue;
                Buffer.MemoryCopy(&defaultValue, target + 1, sizeof(T), sizeof(T));
                int offset = 1 + sizeof(T);
                foreach (var kvp in chunk.tiles)
                {
                    if (packedXYZ)
                    {
                        int x = Convert.ToInt32(kvp.Key.Item1);
                        int y = Convert.ToInt32(kvp.Key.Item2);
                        int z = Convert.ToInt32(kvp.Key.Item3);
                        if ((uint)x > 15 || (uint)y > 15 || (uint)z > 15)
                            throw new ArgumentException("Packed XYZ coordinate out of range", nameof(chunk));
                        ushort coordinates = (ushort)((x << 12) | (y << 8) | (z << 4));
                        target[offset] = (byte)coordinates;
                        target[offset + 1] = (byte)(coordinates >> 8);
                    }
                    else
                    {
                        K x = kvp.Key.Item1;
                        K y = kvp.Key.Item2;
                        K z = kvp.Key.Item3;
                        Buffer.MemoryCopy(&x, target + offset, keySize, keySize);
                        Buffer.MemoryCopy(&y, target + offset + keySize, keySize, keySize);
                        Buffer.MemoryCopy(&z, target + offset + 2 * keySize, keySize, keySize);
                    }
                    T value = kvp.Value;
                    Buffer.MemoryCopy(&value, target + offset + coordinateSize, sizeof(T), sizeof(T));
                    offset += entrySize;
                }
            }
            return data;
        }

    }
}
