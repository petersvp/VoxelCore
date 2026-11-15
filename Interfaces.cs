using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    /// <summary>The value-level API shared by dense and sparse 2D chunks.</summary>
    /// <example><![CDATA[
    /// IChunk2D<int> chunk = new SparseChunk2D<byte, int>(16, 0);
    /// chunk[1, 2] = 7;
    /// ]]></example>
    public interface IChunk2D<T> where T : unmanaged
    {
        /// <summary>Side length of the square chunk.</summary>
        int Size { get; }
        /// <summary>Value used for cells without stored data.</summary>
        T DefaultValue { get; set; }
        /// <summary>Reads or writes a cell by local coordinates.</summary>
        T this[int x, int y] { get; set; }
        /// <summary>Queries a local cell and reports whether its value is available in storage.</summary>
        bool TryGet(int x, int y, out T value);
        /// <summary>Attempts to write a local cell; result semantics depend on the chunk storage type.</summary>
        bool TrySet(int x, int y, T value);
        /// <summary>Caller-owned metadata that follows chunk conversions.</summary>
        object userdata { get; set; }

        /// <summary>Enumerates local cells according to the concrete chunk's includeDefault behavior.</summary>
        /// <remarks>SparseChunk2D currently yields stored entries even when includeDefault is true.</remarks>
        IEnumerable<(int x, int y, T value)> Enumerate(bool includeDefault = false);
    }

    /// <summary>The value-level API shared by dense and sparse 3D chunks.</summary>
    /// <example><![CDATA[
    /// IChunk3D<bool> chunk = new SparseChunk3D<byte, bool>(16, false);
    /// chunk[1, 2, 3] = true;
    /// ]]></example>
    public interface IChunk3D<T> where T : unmanaged
    {
        /// <summary>Side length of the cubic chunk.</summary>
        int Size { get; }
        /// <summary>Value used for cells without stored data.</summary>
        T DefaultValue { get; set; }
        /// <summary>Reads or writes a cell by local coordinates.</summary>
        T this[int x, int y, int z] { get; set; }
        /// <summary>Queries a local cell and reports whether its value is available in storage.</summary>
        bool TryGet(int x, int y, int z, out T value);
        /// <summary>Attempts to write a local cell; result semantics depend on the chunk storage type.</summary>
        bool TrySet(int x, int y, int z, T value);
        /// <summary>Caller-owned metadata that follows chunk conversions.</summary>
        object userdata { get; set; }
        /// <summary>Enumerates stored or all local cells according to includeDefault.</summary>
        IEnumerable<(int x, int y, int z, T value)> Enumerate(bool includeDefault = false);
    }

}
