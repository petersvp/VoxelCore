using System.Collections;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>Inclusive integer bounds and an iterator over every point in a box.</summary>
    /// <remarks>Contains and enumeration include each maximum; width, height, and depth are max minus min.</remarks>
    /// <example><![CDATA[
    /// var bounds = new Range3D(-1, 1, 0, 1, -1, 1);
    /// foreach (var point in bounds) { /* point.x, point.y, point.z are inside. */ }
    /// ]]></example>
    public struct Range3D : IEnumerable<(int x, int y, int z)>
    {
        /// <summary>Inclusive X, Y, and Z bounds.</summary>
        public int minX, maxX, minY, maxY, minZ, maxZ;

        // Default constructor (creates an empty range) [Unity not supported]
        // public Range3D() { minX = maxX = minY = maxY = minZ = maxZ = 0; }

        /// <summary>Creates inclusive bounds from the supplied minima and maxima.</summary>
        public Range3D(int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
        {
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
            this.minZ = minZ;
            this.maxZ = maxZ;
        }

        /// <summary>Moves every minimum down and maximum up by size.</summary>
        public void Extend(int size)
        {
            minX -= size;
            maxX += size;
            minY -= size;
            maxY += size;
            minZ -= size;
            maxZ += size;
        }

        /// <summary>Moves every minimum up and maximum down by size.</summary>
        public void Shrink(int size)
        {
            minX += size;
            maxX -= size;
            minY += size;
            maxY -= size;
            minZ += size;
            maxZ -= size;
        }

        /// <summary>Integer midpoint on X.</summary>
        public int centerX => (minX + maxX) / 2;
        /// <summary>Integer midpoint on Y.</summary>
        public int centerY => (minY + maxY) / 2;
        /// <summary>Integer midpoint on Z.</summary>
        public int centerZ => (minZ + maxZ) / 2;
        /// <summary>maxX minus minX, not the inclusive cell count.</summary>
        public int width => maxX - minX;
        /// <summary>maxY minus minY, not the inclusive cell count.</summary>
        public int height => maxY - minY;
        /// <summary>maxZ minus minZ, not the inclusive cell count.</summary>
        public int depth => maxZ - minZ;
        /// <summary>Product of width, height, and depth.</summary>
        public int volume => width * height * depth;
        // Surface area and total length of the twelve box edges.
        /// <summary>Twice the sum of pairwise dimension products.</summary>
        public int area => 2 * (width * height + width * depth + height * depth);
        /// <summary>Four times the sum of width, height, and depth.</summary>
        public int perimeter => 4 * (width + height + depth);

        /// <summary>Returns whether an integer point lies within the inclusive bounds.</summary>
        public bool Contains(int x, int y, int z)
        {
            return x >= minX && x <= maxX && y >= minY && y <= maxY && z >= minZ && z <= maxZ;
        }
        /// <summary>Returns whether a tuple point lies within the inclusive bounds.</summary>
        public bool Contains((int x, int y, int z) point) => Contains(point.x, point.y, point.z);

        /// <summary>Yields every integer point, with X varying fastest.</summary>
        public IEnumerator<(int x, int y, int z)> GetEnumerator()
        {
            for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                        yield return (x, y, z);
        }

        /// <summary>Returns the same point iterator through non-generic IEnumerable.</summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Formats the X, Y, and Z bounds as ranges.</summary>
        public override string ToString()
        {
            return $"X={minX}..{maxX}; Y={minY}..{maxY}; Z={minZ}..{maxZ}";
        }
    }
}
