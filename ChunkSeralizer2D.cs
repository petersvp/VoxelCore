using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    /// <summary>Writes a 2D chunk as dense, sparse, or RLE bytes, with optional value replacements.</summary>
    /// <remarks>SerializeOptimal with exact=true builds dense, sparse, and RLE encodings and returns the smallest.</remarks>
    /// <example><![CDATA[
    /// IChunk2D<int> chunk = new SparseChunk2D<byte, int>(16, 0);
    /// chunk[1, 2] = 7;
    /// byte[] data = new ChunkSerializer2D<byte, int>().SerializeOptimal(chunk, true);
    /// ]]></example>
    public class ChunkSerializer2D<K, T> where K : unmanaged where T : unmanaged
    {
        // If replacementTable is set, while serializing,
        // replace source chunk values with the values in the table 
        // so e.g. air with value 0 can become ' ' (32) in the serialized data
        // Key: source value, Value: replacement value
        Dictionary<T, T>? replacementTable = null;
        private readonly ChunkBlitSerializer2D<K, T> blit = new();

        /// <summary>Creates a serializer that writes values unchanged.</summary>
        public ChunkSerializer2D() { replacementTable = null; }
        /// <summary>Creates a serializer that substitutes values through the supplied table while writing.</summary>
        /// <param name="replacement">Maps source values to serialized values; null writes them unchanged.</param>
        public ChunkSerializer2D(Dictionary<T, T>? replacement)
        {
            replacementTable = replacement;
        }

        private T Replace(T value)
        {
            return replacementTable != null && replacementTable.TryGetValue(value, out T replaced)
                ? replaced : value;
        }

        /// <summary>Writes every cell as a whole raw T value in x-first order.</summary>
        /// <remarks>A dense chunk without an allocated buffer uses the default-only format instead.</remarks>
        public unsafe byte[] SerializeDense(IChunk2D<T> chunk)
        {
            if (chunk is DenseChunk2D<K, T> emptyDense && emptyDense.tiles == null)
                return blit.SerializeEmptyDense(Replace(chunk.DefaultValue));
            if (replacementTable == null && chunk is DenseChunk2D<K, T> dense)
                return blit.SerializeDense(dense);

            int cells = checked(chunk.Size * chunk.Size);
            byte[] data = new byte[checked(1 + sizeof(T) + cells * sizeof(T))];
            fixed (byte* target = data)
            {
                target[0] = 0;
                T defaultValue = Replace(chunk.DefaultValue);
                Buffer.MemoryCopy(&defaultValue, target + 1, sizeof(T), sizeof(T));
                byte* current = target + 1 + sizeof(T);
                for (int y = 0; y < chunk.Size; y++)
                    for (int x = 0; x < chunk.Size; x++)
                    {
                        T value = Replace(chunk[x, y]);
                        Buffer.MemoryCopy(&value, current, sizeof(T), sizeof(T));
                        current += sizeof(T);
                    }
            }
            return data;
        }

        /// <summary>Writes non-default cells and their local coordinates.</summary>
        /// <param name="packedXY">If true, packs X and Y into one byte; requires a size-16 chunk.</param>
        public unsafe byte[] SerializeSparse(IChunk2D<T> chunk, bool packedXY)
        {
            if (packedXY && chunk.Size != 16)
                throw new ArgumentException("Packed XY requires a chunk size of 16", nameof(chunk));
            if (chunk is DenseChunk2D<K, T> emptyDense && emptyDense.tiles == null)
                return blit.SerializeEmptyDense(Replace(chunk.DefaultValue));
            if (replacementTable == null && chunk is SparseChunk2D<K, T> sparse)
                return blit.SerializeSparse(sparse, packedXY);

            var converted = new SparseChunk2D<K, T>(chunk.Size, Replace(chunk.DefaultValue));
            foreach (var (x, y, value) in chunk.Enumerate())
                converted[x, y] = Replace(value);
            return blit.SerializeSparse(converted, packedXY);
        }

        /// <summary>Writes runs of equal whole T values in x-first cell order.</summary>
        public unsafe byte[] SerializeRLE(IChunk2D<T> chunk)
        {
            if (chunk is DenseChunk2D<K, T> emptyDense && emptyDense.tiles == null)
                return blit.SerializeEmptyDense(Replace(chunk.DefaultValue));

            int cells = checked(chunk.Size * chunk.Size);
            return RLEEncoder.Serialize(EnumerateValues(chunk), Replace(chunk.DefaultValue), cells);
        }

        private IEnumerable<T> EnumerateValues(IChunk2D<T> chunk)
        {
            for (int y = 0; y < chunk.Size; y++)
                for (int x = 0; x < chunk.Size; x++)
                    yield return Replace(chunk[x, y]);
        }

        /// <summary>Selects a chunk encoding and returns its bytes.</summary>
        /// <param name="exact">False estimates dense versus sparse size without building RLE. True serializes dense, sparse, and RLE and returns the shortest byte array.</param>
        public unsafe byte[] SerializeOptimal(IChunk2D<T> chunk, bool exact)
        {
            if (!exact)
            {
                T defaultValue = Replace(chunk.DefaultValue);
                int stored = 0;
                if (replacementTable == null && chunk is SparseChunk2D<K, T> knownSparse)
                    stored = knownSparse.tiles.Count;
                else
                    foreach (var (_, _, value) in chunk.Enumerate())
                        if (!Replace(value).Equals(defaultValue)) stored++;

                bool packedXY = chunk.Size == 16;
                long denseBytes = (long)chunk.Size * chunk.Size * sizeof(T);
                long sparseBytes = (long)stored * ((packedXY ? sizeof(byte) : 2 * sizeof(K)) + sizeof(T));
                return sparseBytes < denseBytes ? SerializeSparse(chunk, packedXY) : SerializeDense(chunk);
            }

            byte[] dense = SerializeDense(chunk);
            byte[] sparse = SerializeSparse(chunk, chunk.Size == 16);
            byte[] rle = SerializeRLE(chunk);
            if (rle.Length < dense.Length && rle.Length < sparse.Length) return rle;
            return sparse.Length < dense.Length ? sparse : dense;
        }
    }
}
