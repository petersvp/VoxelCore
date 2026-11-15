using System;
using System.Collections.Generic;
using System.IO;

namespace PiDev.VoxelCore
{
    public static unsafe class RLEEncoder
    {
        private static int PrefixBytes(int cells)
        {
            uint value = (uint)(cells - 1);
            int bytes = 1;
            while (value > byte.MaxValue) { value >>= 8; bytes++; }
            return bytes;
        }

        private static void WriteCount(MemoryStream output, int length, int bytes)
        {
            uint value = (uint)(length - 1);
            for (int i = 0; i < bytes; i++)
            {
                output.WriteByte((byte)value);
                value >>= 8;
            }
        }

        private static void WriteValue<T>(MemoryStream output, T value) where T : unmanaged
        {
            byte* source = (byte*)&value;
            for (int i = 0; i < sizeof(T); i++) output.WriteByte(source[i]);
        }

        private static void WriteGroup<T>(MemoryStream output, int* lengths, T* values, int groupCount) where T : unmanaged
        {
            byte control = 0;
            for (int i = 0; i < groupCount; i++)
                if (lengths[i] > 256) control |= (byte)(1 << i);
            output.WriteByte(control);
            for (int i = 0; i < groupCount; i++)
                WriteCount(output, lengths[i], (control & (1 << i)) != 0 ? 2 : 1);
            for (int i = 0; i < groupCount; i++) WriteValue(output, values[i]);
        }

        private static void WriteRun<T>(MemoryStream output, int length, T value, bool grouped,
            int prefixBytes, int* lengths, T* values, ref int groupCount) where T : unmanaged
        {
            if (!grouped)
            {
                WriteCount(output, length, prefixBytes);
                WriteValue(output, value);
                return;
            }

            lengths[groupCount] = length;
            values[groupCount++] = value;
            if (groupCount == 8)
            {
                WriteGroup(output, lengths, values, groupCount);
                groupCount = 0;
            }
        }

        private static int ReadCount(byte* source, int cells, int bytes)
        {
            uint value = 0;
            for (int i = 0; i < bytes; i++)
                value |= (uint)source[i] << (i * 8);
            if (value >= (uint)cells) throw new ArgumentException("Invalid RLE run length");
            return (int)value + 1;
        }

        public static byte[] Serialize<T>(IEnumerable<T> values, T defaultValue, int cells) where T : unmanaged
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (cells <= 0) throw new ArgumentOutOfRangeException(nameof(cells));
            bool grouped = cells > byte.MaxValue + 1 && cells <= ushort.MaxValue + 1;
            int prefixBytes = PrefixBytes(cells);
            using var output = new MemoryStream();
            output.WriteByte(3);
            WriteValue(output, defaultValue);

            int* lengths = stackalloc int[8];
            T* groupValues = stackalloc T[8];
            int groupCount = 0;
            int consumed = 0;
            int runLength = 0;
            T previous = default;
            foreach (T value in values)
            {
                if (consumed == cells)
                    throw new ArgumentException("RLE source has more values than the chunk contains", nameof(values));
                consumed++;
                if (runLength == 0)
                {
                    previous = value;
                    runLength = 1;
                }
                else if (value.Equals(previous)) runLength++;
                else
                {
                    WriteRun(output, runLength, previous, grouped, prefixBytes, lengths, groupValues, ref groupCount);
                    previous = value;
                    runLength = 1;
                }
            }
            if (consumed != cells)
                throw new ArgumentException("RLE source has fewer values than the chunk contains", nameof(values));
            WriteRun(output, runLength, previous, grouped, prefixBytes, lengths, groupValues, ref groupCount);
            if (groupCount != 0) WriteGroup(output, lengths, groupValues, groupCount);
            return output.ToArray();
        }

        public static void Deserialize<T>(byte* source, int length, int cells, T* target) where T : unmanaged
        {
            int offset = 1 + sizeof(T);
            int index = 0;
            bool grouped = cells > byte.MaxValue + 1 && cells <= ushort.MaxValue + 1;
            int prefixBytes = PrefixBytes(cells);

            if (grouped)
            {
                while (offset < length)
                {
                    byte control = source[offset++];
                    int remaining = length - offset;
                    int fullBytes = 8 * sizeof(T);
                    for (int j = 0; j < 8; j++)
                        fullBytes += (control & (1 << j)) != 0 ? 2 : 1;

                    int groupCount = 8;
                    if (remaining < fullBytes)
                    {
                        groupCount = 0;
                        int partialBytes = 0;
                        for (int j = 0; j < 8; j++)
                        {
                            partialBytes += sizeof(T) + ((control & (1 << j)) != 0 ? 2 : 1);
                            if (partialBytes == remaining) { groupCount = j + 1; break; }
                        }
                        if (groupCount == 0 || (control >> groupCount) != 0)
                            throw new ArgumentException("Invalid RLE group length");
                    }

                    int* counts = stackalloc int[8];
                    for (int j = 0; j < groupCount; j++)
                    {
                        int bytes = (control & (1 << j)) != 0 ? 2 : 1;
                        counts[j] = ReadCount(source + offset, cells, bytes);
                        offset += bytes;
                    }
                    for (int j = 0; j < groupCount; j++)
                    {
                        int count = counts[j];
                        if (count > cells - index)
                            throw new ArgumentException("Invalid RLE run length");
                        T value = default;
                        Buffer.MemoryCopy(source + offset, &value, sizeof(T), sizeof(T));
                        offset += sizeof(T);
                        int end = index + count;
                        while (index < end) target[index++] = value;
                    }
                }
            }
            else
            {
                while (offset < length)
                {
                    if (length - offset < prefixBytes + sizeof(T))
                        throw new ArgumentException("Incomplete RLE run");
                    int count = ReadCount(source + offset, cells, prefixBytes);
                    offset += prefixBytes;
                    if (count > cells - index)
                        throw new ArgumentException("Invalid RLE run length");
                    T value = default;
                    Buffer.MemoryCopy(source + offset, &value, sizeof(T), sizeof(T));
                    offset += sizeof(T);
                    int end = index + count;
                    while (index < end) target[index++] = value;
                }
            }
            if (index != cells) throw new ArgumentException("Incomplete RLE chunk");
        }
    }
}
