using System.Collections;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    public struct Range3D : IEnumerable<(int x, int y, int z)>
    {
        public int minX, maxX, minY, maxY, minZ, maxZ;

        // Default constructor (creates an empty range) [Unity not supported]
        // public Range3D() { minX = maxX = minY = maxY = minZ = maxZ = 0; }

        public Range3D(int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
        {
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
            this.minZ = minZ;
            this.maxZ = maxZ;
        }

        public IEnumerator<(int x, int y, int z)> GetEnumerator()
        {
            for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                        yield return (x, y, z);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
