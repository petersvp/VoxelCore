using System;

namespace PiDev.VoxelCore
{
    /// <summary>Writes concrete 2D chunks directly from their unmanaged storage.</summary>
    /// <remarks>No value replacement occurs. A dense chunk without a buffer uses the default-only format.</remarks>
    /// <example><![CDATA[
    /// var chunk = new DenseChunk2DFast<byte, int>(16, 0);
    /// byte[] data = new ChunkBlitSerializer2D<byte, int>().SerializeDense(chunk);
    /// ]]></example>
    public unsafe class ChunkBlitSerializer2D<K, T> where K : unmanaged where T : unmanaged
    {
        /// <summary>Copies a dense buffer as raw T values, or writes default-only bytes when it is null.</summary>
        public byte[] SerializeDense(DenseChunk2DFast<K, T> chunk)
        {
            if (chunk.tiles == null) return SerializeEmptyDense(chunk.DefaultValue);

            int cells = checked(chunk.Size * chunk.Size);
            int bytes = checked(cells * sizeof(T));
            byte[] data = new byte[checked(1 + sizeof(T) + bytes)];
            fixed (byte* target = data)
            {
                target[0] = 0;
                T defaultValue = chunk.DefaultValue;
                Buffer.MemoryCopy(&defaultValue, target + 1, sizeof(T), sizeof(T));
                Buffer.MemoryCopy(chunk.tiles, target + 1 + sizeof(T), bytes, bytes);
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

        // Format 1: raw K x/y. Format 2: packed [xxxx yyyy] for size-16 chunks.
        /// <summary>Copies every sparse entry, using raw K coordinates when any entry cannot be packed.</summary>
        /// <param name="packedXY">Requests packed X and Y for a size-16 chunk when all coordinates fit.</param>
        public byte[] SerializeSparse(SparseChunk2DFast<K, T> chunk, bool packedXY = false)
        {
            if (packedXY && chunk.Size != 16)
                throw new ArgumentException("Packed XY requires a chunk size of 16", nameof(chunk));
            var entries = chunk.tiles.ToArray();
            if (packedXY)
                foreach (var entry in entries)
                    if ((uint)Convert.ToInt32(entry.Key.Item1) > 15 ||
                        (uint)Convert.ToInt32(entry.Key.Item2) > 15)
                    {
                        packedXY = false;
                        break;
                    }
            int keySize = sizeof(K);
            int coordinateSize = packedXY ? sizeof(byte) : 2 * keySize;
            int entrySize = checked(coordinateSize + sizeof(T));
            byte[] data = new byte[checked(1 + sizeof(T) + entries.Length * entrySize)];
            fixed (byte* target = data)
            {
                target[0] = (byte)(packedXY ? 2 : 1);
                T defaultValue = chunk.DefaultValue;
                Buffer.MemoryCopy(&defaultValue, target + 1, sizeof(T), sizeof(T));
                int offset = 1 + sizeof(T);
                foreach (var kvp in entries)
                {
                    if (packedXY)
                    {
                        int x = Convert.ToInt32(kvp.Key.Item1);
                        int y = Convert.ToInt32(kvp.Key.Item2);
                        target[offset] = (byte)((x << 4) | y);
                    }
                    else
                    {
                        K x = kvp.Key.Item1;
                        K y = kvp.Key.Item2;
                        Buffer.MemoryCopy(&x, target + offset, keySize, keySize);
                        Buffer.MemoryCopy(&y, target + offset + keySize, keySize, keySize);
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
