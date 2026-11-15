using System;
using System.Runtime.InteropServices;

namespace PiDev.VoxelCore
{
    /// <summary>Reads the 3D chunk formats written by the generic and blit serializers.</summary>
    /// <remarks>The caller supplies the chunk size; it is not stored in the byte array. Pass optimize explicitly.</remarks>
    /// <example><![CDATA[
    /// var original = new SparseChunk3D<byte, int>(16, 0);
    /// byte[] data = new ChunkSerializer3D<byte, int>().SerializeOptimal(original, true);
    /// IChunk3D<int> restored = new ChunkDeserializer3D<byte, int>(16).Deserialize(data, false);
    /// ]]></example>
    public unsafe class ChunkDeserializer3D<K, T> where K : unmanaged where T : unmanaged
    {
        private readonly int chunkSize;

        /// <summary>Creates a reader for chunks with the supplied side length.</summary>
        /// <param name="chunkSize">Required because serialized chunk bytes do not contain their size.</param>
        public ChunkDeserializer3D(int chunkSize)
        {
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            this.chunkSize = chunkSize;
        }

        /// <summary>Reads a dense, sparse, RLE, or default-only 3D chunk.</summary>
        /// <param name="data">Bytes written for the same K, T, and chunk size.</param>
        /// <param name="optimize">True calls the decoded chunk's Optimize() and uses a replacement when returned; false keeps the encoded storage type. A null optimization result leaves the decoded chunk in place.</param>
        public IChunk3DFast<T> Deserialize(byte[] data, bool optimize)
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
                        return new DenseChunk3DFast<K, T>(chunkSize, defaultValue);
                    default: throw new ArgumentException("Unknown chunk format", nameof(data));
                }
            }
        }

        private IChunk3DFast<T> DeserializeDense(byte[] data, byte* source, T defaultValue, bool optimize)
        {
            int cells = checked(chunkSize * chunkSize * chunkSize);
            int bytes = checked(cells * sizeof(T));
            if (data.Length != checked(1 + sizeof(T) + bytes))
                throw new ArgumentException("Invalid dense chunk length", nameof(data));

            var chunk = new DenseChunk3DFast<K, T>(chunkSize, defaultValue);
            chunk.tiles = (T*)Marshal.AllocHGlobal(bytes);
            Buffer.MemoryCopy(source + 1 + sizeof(T), chunk.tiles, bytes, bytes);
            return optimize ? chunk.Optimize() ?? chunk : chunk;
        }

        private IChunk3DFast<T> DeserializeSparse(byte[] data, byte* source, T defaultValue, bool packedXYZ, bool optimize)
        {
            if (packedXYZ && chunkSize != 16)
                throw new ArgumentException("Packed XYZ requires a chunk size of 16", nameof(data));
            int keySize = sizeof(K);
            int coordinateSize = packedXYZ ? sizeof(ushort) : 3 * keySize;
            int entrySize = coordinateSize + sizeof(T);
            int offset = 1 + sizeof(T);
            if ((data.Length - offset) % entrySize != 0)
                throw new ArgumentException("Invalid sparse chunk length", nameof(data));

            var chunk = new SparseChunk3DFast<K, T>(chunkSize, defaultValue);
            while (offset < data.Length)
            {
                int x, y, z;
                if (packedXYZ)
                {
                    ushort coordinates = (ushort)(source[offset] | source[offset + 1] << 8);
                    if ((coordinates & 0xF) != 0)
                        throw new ArgumentException("Invalid packed XYZ coordinate", nameof(data));
                    x = (coordinates >> 12) & 0xF;
                    y = (coordinates >> 8) & 0xF;
                    z = (coordinates >> 4) & 0xF;
                }
                else
                {
                    K xKey = default;
                    K yKey = default;
                    K zKey = default;
                    Buffer.MemoryCopy(source + offset, &xKey, keySize, keySize);
                    Buffer.MemoryCopy(source + offset + keySize, &yKey, keySize, keySize);
                    Buffer.MemoryCopy(source + offset + 2 * keySize, &zKey, keySize, keySize);
                    x = (int)Convert.ChangeType(xKey, typeof(int));
                    y = (int)Convert.ChangeType(yKey, typeof(int));
                    z = (int)Convert.ChangeType(zKey, typeof(int));
                }
                if ((uint)x >= (uint)chunkSize || (uint)y >= (uint)chunkSize || (uint)z >= (uint)chunkSize)
                    throw new ArgumentException("Sparse chunk coordinate out of range", nameof(data));
                T value = default;
                Buffer.MemoryCopy(source + offset + coordinateSize, &value, sizeof(T), sizeof(T));
                chunk[x, y, z] = value;
                offset += entrySize;
            }
            return optimize ? chunk.Optimize() ?? chunk : chunk;
        }

        private IChunk3DFast<T> DeserializeRLE(byte[] data, byte* source, T defaultValue, bool optimize)
        {
            int cells = checked(chunkSize * chunkSize * chunkSize);
            var chunk = new DenseChunk3DFast<K, T>(chunkSize, defaultValue);
            chunk.tiles = (T*)Marshal.AllocHGlobal(checked(cells * sizeof(T)));
            RLEEncoder.Deserialize(source, data.Length, cells, chunk.tiles);
            return optimize ? chunk.Optimize() ?? chunk : chunk;
        }
    }
}
