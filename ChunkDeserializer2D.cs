using System;
using System.Collections.Generic;
using System.Linq;

namespace PiDev.VoxelCore
{
    public class ChunkDeserializer2D<K, T> where K : unmanaged where T : unmanaged
    {
        private readonly int chunkSize;

        public ChunkDeserializer2D(int chunkSize)
        {
            this.chunkSize = chunkSize;
        }

        public IChunk2D<T> Deserialize(byte[] data, bool optimize)
        {
            if (data.Length < 1)
                throw new ArgumentException("Invalid data format");

            int format = data[0];

            switch (format)
            {
                case 0: // Dense format
                case 3: // RLE format (Run-Length Encoding)
                    return DeserializeDense(data, optimize);

                case 1: // Sparse format (based on default values)
                case 2: // Sparse format (packed XY)
                    return DeserializeSparse(data, format == 2, optimize);

                default:
                    throw new ArgumentException("Unknown chunk format");
            }
        }

        private IChunk2D<T> DeserializeDense(byte[] data, bool optimize)
        {
            if (data.Length - 1 != chunkSize * chunkSize)
                throw new ArgumentException("Invalid data length for expected chunk size");

            var denseChunk = new DenseChunk2D<K, T>(chunkSize);

            int index = 1;
            for (int i = 0; i < chunkSize * chunkSize; i++)
            {
                int x = i % chunkSize;
                int y = i / chunkSize;
                denseChunk[x, y] = (T)(object)data[index++];
            }

            if (optimize)
            {
                var optimized = denseChunk.Optimize();
                if (optimized != null) return optimized;
                else return denseChunk;
            }
            return denseChunk;
        }

        private SparseChunk2D<K, T> DeserializeSparse(byte[] data, bool packedXY, bool optimize)
        {
            T borderValue = (T)(object)data[1];
            var sparseChunk = new SparseChunk2D<K, T>(chunkSize, borderValue);

            int index = 2;
            while (index < data.Length)
            {
                int x, y;
                if (packedXY)
                {
                    byte xy = data[index++];
                    x = (xy >> 4) & 0xF;
                    y = xy & 0xF;
                }
                else
                {
                    x = data[index++];
                    y = data[index++];
                }

                T value = (T)(object)data[index++];
                sparseChunk[x, y] = value;
            }

            return optimize ? sparseChunk.ToDenseChunk().Optimize() ?? sparseChunk : sparseChunk;
        }
    }
}
