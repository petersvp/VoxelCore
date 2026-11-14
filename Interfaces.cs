using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    public interface IChunk2D<T> where T : unmanaged
    {
        int Size { get; }
        T DefaultValue { get; set; }
        T this[int x, int y] { get; set; }
        bool TryGet(int x, int y, out T value);
        bool TrySet(int x, int y, T value);
        object userdata { get; set; }

        // If includeDefault=false, yield only non-default / explicitly stored values (best for sparse).
        // If includeDefault=true, yield all Size*Size cells (best for dense or debugging).
        IEnumerable<(int x, int y, T value)> Enumerate(bool includeDefault = false);
    }

}
