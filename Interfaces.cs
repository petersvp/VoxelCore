using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    /// <summary>Reads or writes a value at 2D coordinates.</summary>
    public interface IVoxelDataAccess2D<TData>
    {
        /// <summary>Reads or writes the value at the specified coordinates.</summary>
        TData this[int x, int y] { get; set; }

        /// <summary>Yields independent cell sequences for storage intersecting an inclusive range.</summary>
        /// <param name="range">Requested coordinates in this accessor's coordinate space.</param>
        /// <param name="exact">True filters yielded cells to the range; false leaves filtering to the caller.</param>
        /// <remarks>World sequences use world coordinates and select chunks by their nominal bounds; standalone chunk sequences use local coordinates. Enumeration includes stored non-default cells only and does not create chunks.</remarks>
        IEnumerable<IEnumerable<(int x, int y, TData value)>> EnumerateRange(Range2D range, bool exact);
    }

    /// <summary>Reads or writes a value at 3D coordinates.</summary>
    public interface IVoxelDataAccess3D<TData>
    {
        /// <summary>Reads or writes the value at the specified coordinates.</summary>
        TData this[int x, int y, int z] { get; set; }

        /// <summary>Yields independent cell sequences for storage intersecting an inclusive range.</summary>
        /// <param name="range">Requested coordinates in this accessor's coordinate space.</param>
        /// <param name="exact">True filters yielded cells to the range; false leaves filtering to the caller.</param>
        /// <remarks>World sequences use world coordinates and select chunks by their nominal bounds; standalone chunk sequences use local coordinates. Enumeration includes stored non-default cells only and does not create chunks.</remarks>
        IEnumerable<IEnumerable<(int x, int y, int z, TData value)>> EnumerateRange(Range3D range, bool exact);
    }

    /// <summary>The value-level API shared by dense and sparse 2D chunks.</summary>
    /// <example><![CDATA[
    /// IChunk2DFast<int> chunk = new SparseChunk2DFast<byte, int>(16, 0);
    /// chunk[1, 2] = 7;
    /// ]]></example>
    public interface IChunk2DFast<TData> : IVoxelDataAccess2D<TData> where TData : unmanaged
    {
        /// <summary>Side length of the square chunk.</summary>
        int Size { get; }
        /// <summary>Value used for cells without stored data.</summary>
        TData DefaultValue { get; set; }
        /// <summary>Queries a local cell and reports whether its value is available in storage.</summary>
        bool TryGet(int x, int y, out TData value);
        /// <summary>Attempts to write a local cell; result semantics depend on the chunk storage type.</summary>
        bool TrySet(int x, int y, TData value);
        /// <summary>Caller-owned metadata that follows chunk conversions.</summary>
        object userdata { get; set; }

        /// <summary>Enumerates local cells according to the concrete chunk's includeDefault behavior.</summary>
        /// <remarks>SparseChunk2DFast currently yields stored entries even when includeDefault is true.</remarks>
        IEnumerable<(int x, int y, TData value)> Enumerate(bool includeDefault = false);
    }

    /// <summary>The value-level API shared by dense and sparse 3D chunks.</summary>
    /// <example><![CDATA[
    /// IChunk3DFast<bool> chunk = new SparseChunk3DFast<byte, bool>(16, false);
    /// chunk[1, 2, 3] = true;
    /// ]]></example>
    public interface IChunk3DFast<T> : IVoxelDataAccess3D<T> where T : unmanaged
    {
        /// <summary>Side length of the cubic chunk.</summary>
        int Size { get; }
        /// <summary>Value used for cells without stored data.</summary>
        T DefaultValue { get; set; }
        /// <summary>Queries a local cell and reports whether its value is available in storage.</summary>
        bool TryGet(int x, int y, int z, out T value);
        /// <summary>Attempts to write a local cell; result semantics depend on the chunk storage type.</summary>
        bool TrySet(int x, int y, int z, T value);
        /// <summary>Caller-owned metadata that follows chunk conversions.</summary>
        object userdata { get; set; }
        /// <summary>Enumerates stored or all local cells according to includeDefault.</summary>
        IEnumerable<(int x, int y, int z, T value)> Enumerate(bool includeDefault = false);
    }

    /// <summary>The managed value-level API shared by dense and sparse 2D chunks.</summary>
    /// <example><![CDATA[
    /// IChunk2D<string> chunk = new SparseChunk2D<byte, string>(16, "empty");
    /// chunk[1, 2] = "stone";
    /// ]]></example>
    public interface IChunk2D<T> : IVoxelDataAccess2D<T>
    {
        /// <summary>Comparer used for defaults, value counting, and optimization.</summary>
        IEqualityComparer<T> Comparer { get; }
        /// <summary>Optional whole-chunk serializer called by Serialize.</summary>
        GenericSerializer<IChunk2D<T>>? Serializer { get; set; }
        /// <summary>Side length of the square chunk.</summary>
        int Size { get; }
        /// <summary>Value used for cells without stored data.</summary>
        T DefaultValue { get; set; }
        /// <summary>Queries a local cell and reports whether its value is available in storage.</summary>
        bool TryGet(int x, int y, out T value);
        /// <summary>Attempts to write a local cell; result semantics depend on the chunk storage type.</summary>
        bool TrySet(int x, int y, T value);
        /// <summary>Caller-owned metadata that follows chunk conversions.</summary>
        object userdata { get; set; }

        /// <summary>Enumerates local cells according to the concrete chunk's includeDefault behavior.</summary>
        /// <remarks>SparseChunk2D currently yields stored entries even when includeDefault is true.</remarks>
        IEnumerable<(int x, int y, T value)> Enumerate(bool includeDefault = false);

        /// <summary>Calls the injected whole-chunk serializer.</summary>
        /// <exception cref="NotSupportedException">No serializer has been supplied.</exception>
        byte[] Serialize();
    }

    /// <summary>The value-level API shared by dense and sparse 3D chunks.</summary>
    /// <example><![CDATA[
    /// IChunk3D<bool> chunk = new SparseChunk3D<byte, bool>(16, false);
    /// chunk[1, 2, 3] = true;
    /// ]]></example>
    public interface IChunk3D<T> : IVoxelDataAccess3D<T>
    {
        /// <summary>Comparer used for defaults, value counting, and optimization.</summary>
        IEqualityComparer<T> Comparer { get; }
        /// <summary>Optional whole-chunk serializer called by Serialize.</summary>
        GenericSerializer<IChunk3D<T>>? Serializer { get; set; }
        /// <summary>Side length of the cubic chunk.</summary>
        int Size { get; }
        /// <summary>Value used for cells without stored data.</summary>
        T DefaultValue { get; set; }
        /// <summary>Queries a local cell and reports whether its value is available in storage.</summary>
        bool TryGet(int x, int y, int z, out T value);
        /// <summary>Attempts to write a local cell; result semantics depend on the chunk storage type.</summary>
        bool TrySet(int x, int y, int z, T value);
        /// <summary>Caller-owned metadata that follows chunk conversions.</summary>
        object userdata { get; set; }
        /// <summary>Enumerates stored or all local cells according to includeDefault.</summary>
        IEnumerable<(int x, int y, int z, T value)> Enumerate(bool includeDefault = false);
        /// <summary>Calls the injected whole-chunk serializer.</summary>
        /// <exception cref="NotSupportedException">No serializer has been supplied.</exception>
        byte[] Serialize();
    }
}
