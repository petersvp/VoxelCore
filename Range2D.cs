using System;
using System.Collections;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    /// <summary>Inclusive integer bounds and an iterator over every point in a rectangle.</summary>
    /// <remarks>Contains and enumeration include maxX/maxY; width and height are max minus min.</remarks>
    /// <example><![CDATA[
    /// var bounds = new Range2D(-1, 1, 0, 1);
    /// foreach (var point in bounds) { /* point.x and point.y are inside the bounds. */ }
    /// ]]></example>
    [Serializable]
    public struct Range2D : IEnumerable<(int x, int y)>
    {
        /// <summary>Inclusive X and Y bounds.</summary>
        public int minX, maxX, minY, maxY;

        // Default constructor (creates an empty range) [Unity not supported]
        // public Range2D() { minX = maxX = minY = maxY = 0; }

        /// <summary>Creates inclusive bounds from the supplied minima and maxima.</summary>
        public Range2D(int minX, int maxX, int minY, int maxY)
        {
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
        }

        /// <summary>Moves every minimum down and maximum up by size.</summary>
        public void Extend(int size)
        {
            minX -= size;
            maxX += size;
            minY -= size;
            maxY += size;
        }

        /// <summary>Moves every minimum up and maximum down by size.</summary>
        public void Shrink(int size)
        {
            minX += size;
            maxX -= size;
            minY += size;
            maxY -= size;
        }

        /// <summary>Integer midpoint on X.</summary>
        public int centerX => (minX + maxX) / 2;
        /// <summary>Integer midpoint on Y.</summary>
        public int centerY => (minY + maxY) / 2;
        /// <summary>maxX minus minX, not the inclusive cell count.</summary>
        public int width => maxX - minX;
        /// <summary>maxY minus minY, not the inclusive cell count.</summary>
        public int height => maxY - minY;
        /// <summary>width multiplied by height.</summary>
        public int area => width * height;
        /// <summary>Twice width plus twice height.</summary>
        public int perimeter => width * 2 + height * 2;

        /// <summary>Returns whether an integer point lies within the inclusive bounds.</summary>
        public bool Contains(int x, int y)
        {
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }
        /// <summary>Returns whether a tuple point lies within the inclusive bounds.</summary>
        public bool Contains((int x, int y) point) => Contains(point.x, point.y);


        /// <summary>Yields every integer point, with X varying fastest.</summary>
        public IEnumerator<(int x, int y)> GetEnumerator()
        {
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    yield return (x, y);
        }

        /// <summary>Returns the same point iterator through non-generic IEnumerable.</summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Formats the X and Y bounds as ranges.</summary>
        public override string ToString()
        {
            return $"X={minX}..{maxX}; Y={minY}..{maxY}";
        }
    }

}
