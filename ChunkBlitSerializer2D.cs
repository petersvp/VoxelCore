using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PiDev.VoxelCore
{
    public unsafe class ChunkBlitSerializer2D<K, T> where K : unmanaged where T : unmanaged
    {
        public byte[] SerializeDense(DenseChunk2D<K,T> chunk)
        {
            int size = chunk.Size;
            int totalSize = 1 + sizeof(T) + (size * size * sizeof(T));
            byte[] data = new byte[totalSize];

            fixed (byte* ptr = data)
            {
                ptr[0] = 0; // Format identifier for Dense
                *(T*)(ptr + 1) = chunk.DefaultValue; // Store default value

                T* tilePtr = (T*)(ptr + 1 + sizeof(T));
                Buffer.MemoryCopy(chunk.tiles, tilePtr, size * size * sizeof(T), size * size * sizeof(T));
            }

            return data;
        }

        public byte[] SerializeSparse(SparseChunk2D<K, T> chunk)
        {
            List<byte> data = new List<byte>();
            data.Add(1); // Format identifier for Sparse
            data.AddRange(ToBytes(chunk.DefaultValue));

            foreach (var kvp in chunk.tiles)
            {
                data.AddRange(ToBytes(kvp.Key));
                data.AddRange(ToBytes(kvp.Value));
            }

            return data.ToArray();
        }

        // Generic method to convert unmanaged types to byte array
        private static byte[] ToBytes<TValue>(TValue value) where TValue : unmanaged
        {
            int size = sizeof(TValue);
            byte[] bytes = new byte[size];

            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    *(TValue*)ptr = value;
                }
            }
            return bytes;
        }
    }
}
