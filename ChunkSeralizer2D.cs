using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    // KEEP COMMENTS [except this one]
    public class ChunkSerializer2D<K, T> where K : unmanaged where T : unmanaged
    {
        // If replacementTable is set, while serializing,
        // replace source chunk values with the values in the table 
        // so e.g. air with value 0 can become ' ' (32) in the serialized data
        // Key: source value, Value: replacement value
        Dictionary<T, T>? replacementTable = null;

        public ChunkSerializer2D() { replacementTable = null; }
        public ChunkSerializer2D(Dictionary<T, T>? replacement)
        {
            replacementTable = replacement;
        }

        public byte[] SerializeDense(IChunk2D<T> chunk)
        {
            // format: [0] flattened tiles array
            int size = chunk.Size;
            byte[] data = new byte[1 + (size * size)];
            data[0] = 0;
            int index = 1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    T value = chunk[x, y];
                    if (replacementTable != null && replacementTable.TryGetValue(value, out T replaced))
                        value = replaced;
                    data[index++] = Convert.ToByte(value);
                }
            }
            return data;
        }

        public byte[] SerializeSparse(IChunk2D<T> chunk, bool packedXY)
        {
            // if packetXY is ignoted and K is not 8-bit, throw ArgumentException
            // format: [1][borderValue] {[x][y][val] for each entry in the sparse dict}
            // format: [2][borderValue] {[XY][val] for each entry in the sparse dict}
            // where [XY] is a single byte with x in the high 4 bits and y in the low 4 bits

            if (packedXY && !Is8BitKey<K>())
                throw new ArgumentException("Packed XY format only supports 8-bit K values");

            SparseChunk2D<K, T> sparse = null;
            // Convert DenseChunk to SparseChunk if needed
            if (chunk is DenseChunk2D<K, T> dense)
            {
                var dominantValue = dense.CountValues().OrderByDescending(x => x.Value).Select(x => x.Key).FirstOrDefault();
                chunk = dense.ToSparseChunk(dominantValue);
            }
            else if (chunk is SparseChunk2D<K, T> s) sparse = s;
            else throw new ArgumentException("Invalid chunk type");

            List<byte> data = new List<byte>();
            data.Add((byte)(packedXY ? 2 : 1));
            data.Add(Convert.ToByte(chunk.DefaultValue));

            foreach (var (key, value) in sparse!.tiles)
            {
                T v = value;
                if (replacementTable != null && replacementTable.TryGetValue(value, out T replaced))
                    v = replaced;

                if (packedXY)
                {
                    byte xy = (byte)(((Convert.ToByte(key.Item1) & 0xF) << 4) | (Convert.ToByte(key.Item2) & 0xF));
                    data.Add(xy);
                }
                else
                {
                    data.Add(Convert.ToByte(key.Item1));
                    data.Add(Convert.ToByte(key.Item2));
                }
                data.Add(Convert.ToByte(v));
            }
            return data.ToArray();
        }

        public byte[] SerializeRLE(IChunk2D<T> chunk)
        {
            // format: [3][defaultValue] {[count][val] for each run}
            List<byte> data = new List<byte> { 3, Convert.ToByte(chunk.DefaultValue) };
            int size = chunk.Size;

            T? prevValue = default;
            int count = 0;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    T value = chunk[x, y];
                    if (replacementTable != null && replacementTable.TryGetValue(value, out T replaced))
                        value = replaced;

                    if (prevValue.Equals(value) && count < 255) count++;
                    else
                    {
                        if (count > 0)
                        {
                            data.Add((byte)count);
                            data.Add(Convert.ToByte(prevValue));
                        }
                        prevValue = value;
                        count = 1;
                    }
                }
            }

            if (count > 0)
            {
                data.Add((byte)count);
                data.Add(Convert.ToByte(prevValue));
            }

            return data.ToArray();
        }

        public byte[] SerializeOptimal(IChunk2D<T> chunk, bool exact)
        {
            // Serialize the chunk in the most optimal format
            // if exact, will n all formats and return the smallest [SLOW]

            DenseChunk2D<K, T> dense;
            SparseChunk2D<K, T> sparse;

            if (chunk is DenseChunk2D<K, T> d)
            {
                dense = d;
                var dominantValue = dense.CountValues().OrderByDescending(x => x.Value).Select(x => x.Key).FirstOrDefault();
                sparse = dense.ToSparseChunk(dominantValue);
            }
            else
            {
                sparse = (SparseChunk2D<K, T>)chunk;
                dense = sparse.ToDenseChunk();
            }
            if (exact)
            {
                byte[] denseData = SerializeDense(dense);
                byte[] sparseData = SerializeSparse(sparse, packedXY: true);
                byte[] rleData = SerializeRLE(dense);

                return (denseData.Length <= sparseData.Length && denseData.Length <= rleData.Length) ? denseData :
                       (sparseData.Length <= rleData.Length) ? sparseData : rleData;
            }
            else
            {
                int estimatedSparseSize = sparse.tiles.Count * 2; // packed XY assumed
                int estimatedRLESize = dense.CountValues().Sum(kvp => 2); // count + value
                int estimatedDenseSize = dense.Size * dense.Size; // size * size

                if (estimatedSparseSize < estimatedDenseSize && estimatedSparseSize < estimatedRLESize)
                    return SerializeSparse(sparse, packedXY: true);
                if (estimatedRLESize < estimatedDenseSize)
                    return SerializeRLE(dense);
                return SerializeDense(dense);
            }
        }

        private static bool Is8BitKey<TK>() where TK : unmanaged
        {
            return typeof(TK) == typeof(byte) || typeof(TK) == typeof(sbyte);
        }
    }
}
