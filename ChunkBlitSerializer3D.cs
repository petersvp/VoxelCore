using System;

namespace PiDev.VoxelCore
{
    public unsafe class ChunkBlitSerializer3D<K, T> where K : unmanaged where T : unmanaged
    {
        // [format][default value][cell data in x, y, z iteration order]
        public byte[] SerializeDense(DenseChunk3D<K, T> chunk)
        {
            int cells = checked(chunk.Size * chunk.Size * chunk.Size);
            int bytes = checked(cells * sizeof(T));
            byte[] data = new byte[checked(1 + sizeof(T) + bytes)];
            fixed (byte* target = data)
            fixed (T* source = &chunk.tiles[0, 0, 0])
            {
                target[0] = 0;
                T defaultValue = chunk.DefaultValue;
                Buffer.MemoryCopy(&defaultValue, target + 1, sizeof(T), sizeof(T));
                Buffer.MemoryCopy(source, target + 1 + sizeof(T), bytes, bytes);
            }
            return data;
        }

        // [format][default value]{[x][y][z][value]}
        public byte[] SerializeSparse(SparseChunk3D<K, T> chunk)
        {
            int keySize = sizeof(K);
            int entrySize = checked(3 * keySize + sizeof(T));
            byte[] data = new byte[checked(1 + sizeof(T) + chunk.tiles.Count * entrySize)];
            fixed (byte* target = data)
            {
                target[0] = 1;
                T defaultValue = chunk.DefaultValue;
                Buffer.MemoryCopy(&defaultValue, target + 1, sizeof(T), sizeof(T));
                int offset = 1 + sizeof(T);
                foreach (var kvp in chunk.tiles)
                {
                    K x = kvp.Key.Item1;
                    K y = kvp.Key.Item2;
                    K z = kvp.Key.Item3;
                    Buffer.MemoryCopy(&x, target + offset, keySize, keySize);
                    Buffer.MemoryCopy(&y, target + offset + keySize, keySize, keySize);
                    Buffer.MemoryCopy(&z, target + offset + 2 * keySize, keySize, keySize);
                    T value = kvp.Value;
                    Buffer.MemoryCopy(&value, target + offset + 3 * keySize, sizeof(T), sizeof(T));
                    offset += entrySize;
                }
            }
            return data;
        }

        internal static void WriteInt32(byte* target, int value)
        {
            target[0] = (byte)value;
            target[1] = (byte)(value >> 8);
            target[2] = (byte)(value >> 16);
            target[3] = (byte)(value >> 24);
        }

        internal static int ReadInt32(byte* source)
        {
            return source[0] | source[1] << 8 | source[2] << 16 | source[3] << 24;
        }
    }
}
