using System;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>Writes a 3D chunk as dense, sparse, or RLE bytes, with optional value replacements.</summary>
    /// <remarks>SerializeOptimal with exact=true builds dense, sparse, and RLE encodings and returns the smallest.</remarks>
    /// <example><![CDATA[
    /// IChunk3DFast<int> chunk = new SparseChunk3DFast<byte, int>(16, 0);
    /// chunk[1, 2, 3] = 7;
    /// byte[] data = new ChunkSerializer3D<byte, int>().SerializeOptimal(chunk, true);
    /// ]]></example>
    public class ChunkSerializer3D<K, T> where K : unmanaged where T : unmanaged
    {
        private readonly Dictionary<T, T>? replacementTable;
        private readonly ChunkBlitSerializer3D<K, T> blit = new();

        /// <summary>Creates a serializer that writes values unchanged.</summary>
        public ChunkSerializer3D() { }

        /// <summary>Creates a serializer that substitutes values through the supplied table while writing.</summary>
        /// <param name="replacement">Maps source values to serialized values; null writes them unchanged.</param>
        public ChunkSerializer3D(Dictionary<T, T>? replacement)
        {
            replacementTable = replacement;
        }

        private T Replace(T value)
        {
            return replacementTable != null && replacementTable.TryGetValue(value, out T replaced)
                ? replaced : value;
        }

        /// <summary>Writes every cell as a whole raw T value with x varying fastest.</summary>
        /// <remarks>A dense chunk without an allocated buffer uses the default-only format instead.</remarks>
        public unsafe byte[] SerializeDense(IChunk3DFast<T> chunk)
        {
            if (chunk is DenseChunk3DFast<K, T> emptyDense && emptyDense.tiles == null)
                return blit.SerializeEmptyDense(Replace(chunk.DefaultValue));
            if (replacementTable == null && chunk is DenseChunk3DFast<K, T> dense)
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

        /// <summary>Writes non-default cells and their local coordinates.</summary>
        /// <param name="packedXYZ">If true, packs XYZ into two bytes; requires a size-16 chunk.</param>
        public unsafe byte[] SerializeSparse(IChunk3DFast<T> chunk, bool packedXYZ = false)
        {
            if (packedXYZ && chunk.Size != 16)
                throw new ArgumentException("Packed XYZ requires a chunk size of 16", nameof(chunk));
            if (chunk is DenseChunk3DFast<K, T> emptyDense && emptyDense.tiles == null)
                return blit.SerializeEmptyDense(Replace(chunk.DefaultValue));
            if (replacementTable == null && chunk is SparseChunk3DFast<K, T> sparse)
                return blit.SerializeSparse(sparse, packedXYZ);

            var converted = new SparseChunk3DFast<K, T>(chunk.Size, Replace(chunk.DefaultValue));
            foreach (var (x, y, z, value) in chunk.Enumerate())
                converted[x, y, z] = Replace(value);
            return blit.SerializeSparse(converted, packedXYZ);
        }

        // [format][default value][size-dependent RLE runs]
        /// <summary>Writes runs of equal whole T values in x-first cell order.</summary>
        public unsafe byte[] SerializeRLE(IChunk3DFast<T> chunk)
        {
            if (chunk is DenseChunk3DFast<K, T> emptyDense && emptyDense.tiles == null)
                return blit.SerializeEmptyDense(Replace(chunk.DefaultValue));
            int cells = checked(chunk.Size * chunk.Size * chunk.Size);
            return RLEEncoder.Serialize(EnumerateValues(chunk), Replace(chunk.DefaultValue), cells);
        }

        private IEnumerable<T> EnumerateValues(IChunk3DFast<T> chunk)
        {
            for (int z = 0; z < chunk.Size; z++)
                for (int y = 0; y < chunk.Size; y++)
                    for (int x = 0; x < chunk.Size; x++)
                        yield return Replace(chunk[x, y, z]);
        }

        /// <summary>Selects a chunk encoding and returns its bytes.</summary>
        /// <param name="exact">False estimates dense versus sparse size without building RLE. True serializes dense, sparse, and RLE and returns the shortest byte array.</param>
        public unsafe byte[] SerializeOptimal(IChunk3DFast<T> chunk, bool exact)
        {
            if (!exact)
            {
                T defaultValue = Replace(chunk.DefaultValue);
                int stored = 0;
                if (replacementTable == null && chunk is SparseChunk3DFast<K, T> knownSparse)
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
