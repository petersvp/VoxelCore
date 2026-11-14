using System;

namespace PiDev.VoxelCore
{
    public unsafe class ChunkDeserializer3D<T> where T : unmanaged
    {
        private readonly int chunkSize;

        public ChunkDeserializer3D(int chunkSize)
        {
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            this.chunkSize = chunkSize;
        }

        public IChunk3D<T> Deserialize(byte[] data, bool optimize = false)
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
                    case 1: return DeserializeSparse(data, source, defaultValue);
                    case 3: return DeserializeRLE(data, source, defaultValue, optimize);
                    default: throw new ArgumentException("Unknown chunk format", nameof(data));
                }
            }
        }

        private IChunk3D<T> DeserializeDense(byte[] data, byte* source, T defaultValue, bool optimize)
        {
            int cells = checked(chunkSize * chunkSize * chunkSize);
            int bytes = checked(cells * sizeof(T));
            if (data.Length != checked(1 + sizeof(T) + bytes))
                throw new ArgumentException("Invalid dense chunk length", nameof(data));

            var chunk = new DenseChunk3D<T>(chunkSize, defaultValue);
            fixed (T* target = &chunk.tiles[0, 0, 0])
                Buffer.MemoryCopy(source + 1 + sizeof(T), target, bytes, bytes);
            return optimize ? Optimize(chunk) : chunk;
        }

        private IChunk3D<T> DeserializeSparse(byte[] data, byte* source, T defaultValue)
        {
            int entrySize = 3 * sizeof(int) + sizeof(T);
            int offset = 1 + sizeof(T);
            if ((data.Length - offset) % entrySize != 0)
                throw new ArgumentException("Invalid sparse chunk length", nameof(data));

            var chunk = new SparseChunk3D<T>(chunkSize, defaultValue);
            while (offset < data.Length)
            {
                int x = ChunkBlitSerializer3D<T>.ReadInt32(source + offset);
                int y = ChunkBlitSerializer3D<T>.ReadInt32(source + offset + 4);
                int z = ChunkBlitSerializer3D<T>.ReadInt32(source + offset + 8);
                if ((uint)x >= (uint)chunkSize || (uint)y >= (uint)chunkSize || (uint)z >= (uint)chunkSize)
                    throw new ArgumentException("Sparse chunk coordinate out of range", nameof(data));
                T value = default;
                Buffer.MemoryCopy(source + offset + 12, &value, sizeof(T), sizeof(T));
                chunk[x, y, z] = value;
                offset += entrySize;
            }
            return chunk;
        }

        private IChunk3D<T> DeserializeRLE(byte[] data, byte* source, T defaultValue, bool optimize)
        {
            int cells = checked(chunkSize * chunkSize * chunkSize);
            int entrySize = sizeof(int) + sizeof(T);
            int offset = 1 + sizeof(T);
            if ((data.Length - offset) % entrySize != 0)
                throw new ArgumentException("Invalid RLE chunk length", nameof(data));

            var chunk = new DenseChunk3D<T>(chunkSize, defaultValue);
            int index = 0;
            fixed (T* target = &chunk.tiles[0, 0, 0])
            {
                while (offset < data.Length)
                {
                    int count = ChunkBlitSerializer3D<T>.ReadInt32(source + offset);
                    if (count <= 0 || count > cells - index)
                        throw new ArgumentException("Invalid RLE run length", nameof(data));
                    T value = default;
                    Buffer.MemoryCopy(source + offset + 4, &value, sizeof(T), sizeof(T));
                    int end = index + count;
                    while (index < end) target[index++] = value;
                    offset += entrySize;
                }
            }
            if (index != cells) throw new ArgumentException("Incomplete RLE chunk", nameof(data));
            return optimize ? Optimize(chunk) : chunk;
        }

        private IChunk3D<T> Optimize(DenseChunk3D<T> chunk)
        {
            var sparse = chunk.ToSparseChunk();
            long sparseBytes = (long)sparse.tiles.Count * (3 * sizeof(int) + sizeof(T));
            long denseBytes = (long)chunkSize * chunkSize * chunkSize * sizeof(T);
            return sparseBytes < denseBytes ? sparse : chunk;
        }
    }
}
