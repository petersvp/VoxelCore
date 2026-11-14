using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    public static class Utilities
    {
        private const short INLINE = 256; // MethodImplOptions.AggressiveInlining;
        private const short OPTIMISE = 512; // MethodImplOptions.AggressiveOptimization;

        // Negative floor division and modulus
        public static int nfdiv(float a, float b)
        {
            return (int)(a > 0 ? a / b : (a - b + 1) / b);
        }
        public static float nfmod(float a, float b)
        {
            return a - b * MathF.Floor(a / b);
        }

        // AB based ranges
        public static float RemapRanges(this float v, float oldMin, float oldMax, float newMin, float newMax)
        {
            return (((v - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
        }
        public static double RemapRanges(this double v, double oldMin, double oldMax, double newMin, double newMax)
        {
            return (((v - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
        }
        public static decimal RemapRanges(this decimal v, decimal oldMin, decimal oldMax, decimal newMin, decimal newMax)
        {
            return (((v - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
        }
        public static long RemapRanges(this long v, long oldMin, long oldMax, long newMin, long newMax)
        {
            return (((v - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
        }
        public static int RemapRanges(this int v, int oldMin, int oldMax, int newMin, int newMax)
        {
            return (int)RemapRanges((float)v, (float)oldMin, (float)oldMax, (float)newMin, (float)newMax);
        }

        public static string StrDensityBlocks = " .,:;-+E#@";
        public static string StrDensityText = ".'`^\",:;Il!i><~+_-?[]{}1()\\|/tfrxnuvcxzYXUJCLQ0OZmwqpdkbhao*#MW&8%B@$";
        public static char ByteToDensity(int b, string gradient) => StrDensityBlocks[((int)b.Clamp(0, 255)).RemapRanges(0,256,0,gradient.Length-1)];
        public static char ByteToDensityText(int b) => StrDensityText[((int)b.Clamp(0, 255)).RemapRanges(0,256,0, StrDensityText.Length)];
        public static char ByteToDensityBlocks(int b) => StrDensityBlocks[((int)b.Clamp(0, 255)).RemapRanges(0,256,0, StrDensityBlocks.Length)];
    
        public static int Clamp(this int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
