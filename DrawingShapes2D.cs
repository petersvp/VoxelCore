using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    /// <summary>Integer point iterators and helpers for drawing 2D lines and splines.</summary>
    /// <example><![CDATA[
    /// var world = new World2D<byte, int>(_ => new SparseChunk2D<byte, int>(16, 0));
    /// foreach (var point in DrawingShapes2D.Line((0, 0), (4, 2)))
    ///     world[point.x, point.y] = 1;
    /// ]]></example>
    public static class DrawingShapes2D
    {

        /// <summary>Samples a rounded Catmull-Rom spline through a list of integer control points.</summary>
        /// <param name="loop">True wraps the control points into a closed path.</param>
        /// <param name="useLengths">True scales sampling density with Manhattan segment length.</param>
        /// <param name="resolution">Samples per segment, or per length unit when useLengths is true.</param>
        public static List<(int x, int y)> GenerateCatmullRomSpline(List<(int x, int y)> controlPoints, bool loop, bool useLengths = false, float resolution = 10)
        {
            if (controlPoints == null || controlPoints.Count < 2)
                return controlPoints?.ToList() ?? new List<(int x, int y)>();

            List<(int x, int y)> splinePoints = new();

            // Ensure proper wrapping for loops
            List<(int x, int y)> dataset = new List<(int x, int y)>(controlPoints);

            if (loop)
            {
                // Properly extend start and end to ensure continuity
                dataset.Insert(0, controlPoints[^1]);  // Prepend second-to-last as P0
                dataset.Insert(0, controlPoints[^2]);  // Prepend second-to-last as P0
                dataset.Add(controlPoints[0]);         // Close loop with first point
                dataset.Add(controlPoints[1]);         // Ensure the last segment transitions correctly
            }
            else
            {
                if (controlPoints.Count < 3) // Special case: Only 2 points available
                    return controlPoints.ToList();

                dataset.Insert(0, controlPoints[0]);  // Extend first control
                dataset.Add(controlPoints[^1]);       // Extend last control
            }

            // Generate spline points
            for (int i = 1; i < dataset.Count - 2; i++)
            {
                var p0 = dataset[i - 1];
                var p1 = dataset[i];
                var p2 = dataset[i + 1];
                var p3 = dataset[i + 2];

                float step = useLengths ? 1f / (Math.Max(1, Distance(p1, p2)) * resolution) : 1f / resolution;
                step = Math.Max(step, float.Epsilon); // Prevent division by zero

                for (float t = 0; t < 1; t += step)
                    splinePoints.Add(CatmullRom(t, p0, p1, p2, p3));

                splinePoints.Add(CatmullRom(1, p0, p1, p2, p3)); // Ensure endpoint
            }

            if (loop)
            {
                splinePoints.Add(splinePoints[0]); // Close the loop
            }

            return splinePoints.Distinct().ToList();
        }

        /// <summary>Interpolates a point on the p1-to-p2 spline segment and rounds it to integers.</summary>
        public static (int x, int y) CatmullRom(float t, (int x, int y) p0, (int x, int y) p1, (int x, int y) p2, (int x, int y) p3)
        {
            float tt = t * t, ttt = tt * t;

            float x = 0.5f * (2 * p1.x + (-p0.x + p2.x) * t + (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * tt + (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * ttt);
            float y = 0.5f * (2 * p1.y + (-p0.y + p2.y) * t + (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * tt + (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * ttt);

            return ((int)Math.Round(x), (int)Math.Round(y));
        }

        /// <summary>Returns Manhattan distance between two integer points.</summary>
        public static int Distance((int x, int y) a, (int x, int y) b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);


        /// <summary>Rasterizes sampled spline segments into unique integer points.</summary>
        /// <remarks>The control-point enumerable is read more than once, so it must be repeatable.</remarks>
        /// <param name="loop">True closes the path between the last and first sampled points.</param>
        /// <param name="useLengths">True scales spline sampling with segment length.</param>
        /// <param name="resolution">Spline sampling density.</param>
        public static IEnumerable<(int x, int y)> SplinePath(IEnumerable<(int x, int y)> controlPoints, bool loop, bool useLengths, int resolution)
        {
            if (controlPoints == null || !controlPoints.Any())
                yield break;

            var splinePoints = GenerateCatmullRomSpline(controlPoints.ToList(), loop, useLengths, resolution);

            HashSet<(int x, int y)> visitedPoints = new();

            for (int i = 0; i < splinePoints.Count - 1 + (loop ? 1 : 0); i++)
            {
                var start = splinePoints[i];
                var end = splinePoints[(i + 1) % splinePoints.Count]; // Ensures looping

                foreach (var point in Line(start, end))
                {
                    if (visitedPoints.Add(point)) // Ensures uniqueness
                        yield return point;
                }
            }
        }

        /// <summary>Yields a raster line between two coordinate pairs, including both endpoints.</summary>
        public static IEnumerable<(int x, int y)> Line(int x0, int y0, int x1, int y1) =>
            Line((x0, y0), (x1, y1));
        /// <summary>Yields a raster line from a tuple containing its endpoints.</summary>
        public static IEnumerable<(int x, int y)> Line(((int x, int y) start, (int x, int y) end) line) =>
            Line(line.start, line.end);
        /// <summary>Yields a raster line between two points, including both endpoints.</summary>
        public static IEnumerable<(int x, int y)> Line((int x, int y) start, (int x, int y) end)
        {
            int x0 = start.x, y0 = start.y;
            int x1 = end.x, y1 = end.y;

            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = (dx > dy ? dx : -dy) / 2, e2;

            while (true)
            {
                yield return (x0, y0);
                if (x0 == x1 && y0 == y1) break;

                e2 = err;
                if (e2 > -dx) { err -= dy; x0 += sx; } // Step in x-direction
                if (e2 < dy) { err += dx; y0 += sy; }  // Step in y-direction
            }
        }



        /// <summary>Returns the first two candidate boundary intersections for a line through the range center.</summary>
        /// <param name="angleDegrees">Line angle in degrees.</param>
        /// <exception cref="InvalidOperationException">Fewer than two candidates are found.</exception>
        public static ((int x, int y) start, (int x, int y) end) GetSplittingLine(Range2D bounds, float angleDegrees)
        {
            double angle = angleDegrees * (Math.PI / 180.0); // Convert degrees to radians
            double cosA = Math.Cos(angle);
            double sinA = Math.Sin(angle);

            // Determine the farthest intersections with the rectangle boundaries
            int[] xCandidates = { bounds.minX, bounds.maxX };
            int[] yCandidates = { bounds.minY, bounds.maxY };

            // Compute candidate boundary intersection points
            List<(int x, int y)> candidates = new List<(int, int)>();

            foreach (var x in xCandidates)
            {
                int y = (int)Math.Round(bounds.centerY + (x - bounds.centerX) * (sinA / cosA));
                if (y >= bounds.minY && y <= bounds.maxY) candidates.Add((x, y));
            }

            foreach (var y in yCandidates)
            {
                int x = (int)Math.Round(bounds.centerX + (y - bounds.centerY) * (cosA / sinA));
                if (x >= bounds.minX && x <= bounds.maxX) candidates.Add((x, y));
            }

            // Return the two farthest points along the given angle
            if (candidates.Count < 2)
                throw new InvalidOperationException("Failed to find two distinct boundary points");

            return (candidates[0], candidates[1]);
        }

    }

}
