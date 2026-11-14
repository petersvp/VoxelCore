using System.Collections;
using System.Collections.Generic;

namespace PiDev.VoxelCore
{
    public struct Range2D : IEnumerable<(int x, int y)>
    {
        public int minX, maxX, minY, maxY;

        // Default constructor (creates an empty range) [Unity not supported]
        // public Range2D() { minX = maxX = minY = maxY = 0; }

        public Range2D(int minX, int maxX, int minY, int maxY)
        {
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
        }

        public void Extend(int size)
        {
            minX -= size;
            maxX += size;
            minY -= size;
            maxY += size;
        }

        public void Shrink(int size)
        {
            minX += size;
            maxX -= size;
            minY += size;
            maxY -= size;
        }

        public int centerX => (minX + maxX) / 2;
        public int centerY => (minY + maxY) / 2;
        public int width => maxX - minX;
        public int height => maxY - minY;
        public int area => width * height;
        public int perimeter => width * 2 + height * 2;

        public bool Contains(int x, int y)
        {
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }
        public bool Contains((int x, int y) point) => Contains(point.x, point.y);


        public IEnumerator<(int x, int y)> GetEnumerator()
        {
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    yield return (x, y);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            return $"X={minX}..{maxX}; Y={minY}..{maxY}";
        }
    }

}
