using System;
using System.Runtime.InteropServices;

namespace PiDev.VoxelCore
{
    public unsafe class ChunkDeserializer2D<K, T> where K : unmanaged where T : unmanaged
    {
        private readonly int chunkSize;

        public ChunkDeserializer2D(int chunkSize)
        {
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            this.chunkSize = chunkSize;
        }

        public IChunk2D<T> Deserialize(byte[] data, bool optimize)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < 1 + sizeof(T)) throw new ArgumentException("Invalid chunk data", nameof(data));

            fixed (byte* source = data)
            {
                T defaultValue = default;
                Buffer.MemoryCopy(source + 1, &defaultValue, sizeof(T), sizeof(T));
                switch (source[0])
                {
                    case 0: return DeserializeDense(data, source, defaultValue, optimize);
                    case 1: return DeserializeSparse(data, source, defaultValue, false, optimize);
                    case 2: return DeserializeSparse(data, source, defaultValue, true, optimize);
                    case 3: return DeserializeRLE(data, source, defaultValue, optimize);
                    case 4:
                        if (data.Length != 1 + sizeof(T))
                            throw new ArgumentException("Invalid empty chunk length", nameof(data));
                        return new DenseChunk2D<K, T>(chunkSize, defaultValue);
                    default: throw new ArgumentException("Unknown chunk format", nameof(data));
                }
            }
        }

        private IChunk2D<T> DeserializeDense(byte[] data, byte* source, T defaultValue, bool optimize)
        {
            int cells = checked(chunkSize * chunkSize);
            int bytes = checked(cells * sizeof(T));
            if (data.Length != checked(1 + sizeof(T) + bytes))
                throw new ArgumentException("Invalid dense chunk length", nameof(data));

            var chunk = new DenseChunk2D<K, T>(chunkSize, defaultValue);
            chunk.tiles = (T*)Marshal.AllocHGlobal(bytes);
            Buffer.MemoryCopy(source + 1 + sizeof(T), chunk.tiles, bytes, bytes);
            return optimize ? chunk.Optimize() ?? chunk : chunk;
        }

        private IChunk2D<T> DeserializeSparse(byte[] data, byte* source, T defaultValue, bool packedXY, bool optimize)
        {
            if (packedXY && chunkSize != 16)
                throw new ArgumentException("Packed XY requires a chunk size of 16", nameof(data));
            int keySize = sizeof(K);
            int coordinateSize = packedXY ? sizeof(byte) : 2 * keySize;
            int entrySize = coordinateSize + sizeof(T);
            int offset = 1 + sizeof(T);
            if ((data.Length - offset) % entrySize != 0)
                throw new ArgumentException("Invalid sparse chunk length", nameof(data));

            var chunk = new SparseChunk2D<K, T>(chunkSize, defaultValue);
            while (offset < data.Length)
            {
                int x, y;
                if (packedXY)
                {
                    x = (source[offset] >> 4) & 0xF;
                    y = source[offset] & 0xF;
                }
                else
                {
                    K xKey = default;
                    K yKey = default;
                    Buffer.MemoryCopy(source + offset, &xKey, keySize, keySize);
                    Buffer.MemoryCopy(source + offset + keySize, &yKey, keySize, keySize);
                    x = (int)Convert.ChangeType(xKey, typeof(int));
                    y = (int)Convert.ChangeType(yKey, typeof(int));
                }
                if ((uint)x >= (uint)chunkSize || (uint)y >= (uint)chunkSize)
                    throw new ArgumentException("Sparse chunk coordinate out of range", nameof(data));
                T value = default;
                Buffer.MemoryCopy(source + offset + coordinateSize, &value, sizeof(T), sizeof(T));
                chunk[x, y] = value;
                offset += entrySize;
            }
            return optimize ? chunk.Optimize() ?? chunk : chunk;
        }

        private IChunk2D<T> DeserializeRLE(byte[] data, byte* source, T defaultValue, bool optimize)
        {
            int cells = checked(chunkSize * chunkSize);
            var chunk = new DenseChunk2D<K, T>(chunkSize, defaultValue);
            chunk.tiles = (T*)Marshal.AllocHGlobal(checked(cells * sizeof(T)));
            RLEEncoder.Deserialize(source, data.Length, cells, chunk.tiles);
            return optimize ? chunk.Optimize() ?? chunk : chunk;
        }
    }
}
