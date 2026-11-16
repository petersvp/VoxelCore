using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    /// <summary>A 2D chunk with a lazily allocated, contiguous unmanaged cell buffer.</summary>
    /// <remarks>Before the first write, every cell reads as <see cref="DefaultValue"/> and no buffer is allocated.</remarks>
    /// <example><![CDATA[
    /// var chunk = new DenseChunk2DFast<byte, int>(16, 0);
    /// chunk[2, 3] = 7;
    /// IChunk2DFast<int>? optimized = chunk.Optimize(); // May be this, a sparse chunk, or null.
    /// ]]></example>
    public unsafe class DenseChunk2DFast<K, T> : IChunk2DFast<T> where K : unmanaged where T : unmanaged
    {
        internal T* tiles;
        private readonly int size;
        /// <summary>Value read from every cell before a buffer is allocated.</summary>
        public T DefaultValue { get; set; }
        /// <summary>Caller-owned metadata carried into converted chunks.</summary>
        public object userdata { get; set; }

        /// <summary>Side length of this square chunk.</summary>
        public int Size => size;

        /// <summary>Creates a chunk whose buffer is initially unallocated.</summary>
        public DenseChunk2DFast(int size = 16, T defaultValue = default)
        {
            this.size = size;
            DefaultValue = defaultValue;
            tiles = null; // Initialize as null to avoid unnecessary allocations
        }

        private int GetIndex(int x, int y) => (y * size) + x;

        /// <summary>Reads or writes a local cell; the first write allocates and fills the buffer.</summary>
        /// <remarks>Out-of-bounds reads return DefaultValue; writes are ignored.</remarks>
        public T this[int x, int y]
        {
            get => (uint)x < (uint)size && (uint)y < (uint)size && tiles != null ? tiles[GetIndex(x, y)] : DefaultValue;
            set
            {
                if ((uint)x >= (uint)size || (uint)y >= (uint)size) return;
                if (tiles == null)
                {
                    tiles = (T*)Marshal.AllocHGlobal(size * size * sizeof(T));
                    for (int i = 0; i < size * size; i++)
                        tiles[i] = DefaultValue;
                }
                tiles[GetIndex(x, y)] = value;
            }
        }

        /// <summary>Reads a cell; returns false with DefaultValue if the buffer is still unallocated.</summary>
        public bool TryGet(int x, int y, out T value)
        {
            if ((uint)x >= (uint)size || (uint)y >= (uint)size || tiles == null)
            {
                value = DefaultValue;
                return false;
            }
            value = tiles[GetIndex(x, y)];
            return true;
        }

        /// <summary>Writes a cell, allocating the buffer if needed, and returns true.</summary>
        public bool TrySet(int x, int y, T value)
        {
            if ((uint)x >= (uint)size || (uint)y >= (uint)size) return false;
            if (tiles == null)
            {
                tiles = (T*)Marshal.AllocHGlobal(size * size * sizeof(T));
                for (int i = 0; i < size * size; i++)
                    tiles[i] = DefaultValue;
            }
            tiles[GetIndex(x, y)] = value;
            return true;
        }

        /// <summary>Counts all allocated cell values; returns an empty map while the buffer is null.</summary>
        public Dictionary<T, int> CountValues()
        {
            Dictionary<T, int> valueCounts = new Dictionary<T, int>();
            if (tiles == null) return valueCounts;
            T* end = tiles + (size * size);
            for (T* current = tiles; current < end; current++)
            {
                T value = *current;
                if (valueCounts.ContainsKey(value))
                    valueCounts[value]++;
                else
                    valueCounts[value] = 1;
            }
            return valueCounts;
        }

        /// <summary>Enumerates local cells, omitting default-valued cells unless requested.</summary>
        /// <param name="includeDefault">True includes every cell; false yields only values different from DefaultValue.</param>
        public IEnumerable<(int x, int y, T value)> Enumerate(bool includeDefault = false)
        {
            var def = DefaultValue;
            int s = Size;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    var v = this[x, y];
                    if (includeDefault || !v.Equals(def))
                        yield return (x, y, v);
                }
        }

        /// <summary>Yields this chunk's local cells, filtering in the inner iterator only when exact is true.</summary>
        public IEnumerable<IEnumerable<(int x, int y, T value)>> EnumerateRange(Range2D range, bool exact)
        {
            yield return exact ? EnumerateRangeExact(range) : Enumerate();
        }

        private IEnumerable<(int x, int y, T value)> EnumerateRangeExact(Range2D range)
        {
            foreach (var cell in Enumerate())
                if (range.Contains(cell.x, cell.y)) yield return cell;
        }

        /// <summary>Copies this chunk into sparse storage using its current DefaultValue.</summary>
        public SparseChunk2DFast<K, T> ToSparseChunk() => ToSparseChunk(DefaultValue);

        /// <summary>Copies this chunk into sparse storage using a specified default value.</summary>
        public SparseChunk2DFast<K, T> ToSparseChunk(T defaultValue)
        {
            var sparseChunk = new SparseChunk2DFast<K, T>(size, defaultValue);
            sparseChunk.userdata = userdata;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    T value = this[x, y];
                    if (!value.Equals(defaultValue)) sparseChunk[x, y] = value;
                }
            }
            return sparseChunk;
        }

        /// <summary>Chooses sparse storage when it is smaller than the dense cell buffer.</summary>
        /// <param name="defaultValue">Target default when supplied; null lets this chunk choose its most common value.</param>
        /// <returns>This if conversion is not smaller; a new sparse chunk if it is; null when every cell matches a supplied default, or an empty chunk already matches its own default.</returns>
        public IChunk2DFast<T>? Optimize(T? defaultValue = null)
        {
            if (tiles == null)
                return !defaultValue.HasValue || defaultValue.Value.Equals(DefaultValue) ? null : this;

            int total = checked(size * size);
            var frequency = CountValues();
            T mostCommonValue = DefaultValue;
            int mostCommonCount = 0;
            foreach (var kvp in frequency)
                if (kvp.Value > mostCommonCount)
                {
                    mostCommonCount = kvp.Value;
                    mostCommonValue = kvp.Key;
                }

            T targetDefault = defaultValue.HasValue ? defaultValue.Value : mostCommonValue;
            frequency.TryGetValue(targetDefault, out int targetCount);
            if (targetCount == total && (defaultValue.HasValue || targetDefault.Equals(DefaultValue))) return null;

            long sparseBytes = (long)(total - targetCount) * (2 * sizeof(K) + sizeof(T));
            long denseBytes = (long)total * sizeof(T);
            if (sparseBytes >= denseBytes) return this;
            return ToSparseChunk(targetDefault);
        }

        ~DenseChunk2DFast()
        {
            if (tiles != null) Marshal.FreeHGlobal((IntPtr)tiles);
        }
    }

}
