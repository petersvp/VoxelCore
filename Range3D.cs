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

        public void Extend(int size)
        {
            minX -= size;
            maxX += size;
            minY -= size;
            maxY += size;
            minZ -= size;
            maxZ += size;
        }

        public void Shrink(int size)
        {
            minX += size;
            maxX -= size;
            minY += size;
            maxY -= size;
            minZ += size;
            maxZ -= size;
        }

        public int centerX => (minX + maxX) / 2;
        public int centerY => (minY + maxY) / 2;
        public int centerZ => (minZ + maxZ) / 2;
        public int width => maxX - minX;
        public int height => maxY - minY;
        public int depth => maxZ - minZ;
        public int volume => width * height * depth;
        // Surface area and total length of the twelve box edges.
        public int area => 2 * (width * height + width * depth + height * depth);
        public int perimeter => 4 * (width + height + depth);

        public bool Contains(int x, int y, int z)
        {
            return x >= minX && x <= maxX && y >= minY && y <= maxY && z >= minZ && z <= maxZ;
        }
        public bool Contains((int x, int y, int z) point) => Contains(point.x, point.y, point.z);

        public IEnumerator<(int x, int y, int z)> GetEnumerator()
        {
            for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                        yield return (x, y, z);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            return $"X={minX}..{maxX}; Y={minY}..{maxY}; Z={minZ}..{maxZ}";
        }
    }
}
