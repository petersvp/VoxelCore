using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PiDev.VoxelCore
{
    /// <summary>Numeric range mapping, clamping, and density-character helpers.</summary>
    /// <remarks>nfdiv/nfmod use floating-point arithmetic; worlds use integer coordinate mapping instead.</remarks>
    /// <example><![CDATA[
    /// int percent = 10.RemapRanges(0, 20, 0, 100); // 50
    /// int bounded = percent.Clamp(0, 100);
    /// ]]></example>
    public static class Utilities
    {
        private const short INLINE = 256; // MethodImplOptions.AggressiveInlining;
        private const short OPTIMISE = 512; // MethodImplOptions.AggressiveOptimization;

        /// <summary>Computes legacy floor-style division through float arithmetic.</summary>
        /// <remarks>Large integer coordinates can lose precision; worlds use integer mapping instead.</remarks>
        public static int nfdiv(float a, float b)
        {
            return (int)(a > 0 ? a / b : (a - b + 1) / b);
        }
        /// <summary>Computes a floor-based floating-point remainder.</summary>
        public static float nfmod(float a, float b)
        {
            return a - b * MathF.Floor(a / b);
        }

        /// <summary>Maps a float from one numeric interval to another.</summary>
        public static float RemapRanges(this float v, float oldMin, float oldMax, float newMin, float newMax)
        {
            return (((v - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
        }
        /// <summary>Maps a double from one numeric interval to another.</summary>
        public static double RemapRanges(this double v, double oldMin, double oldMax, double newMin, double newMax)
        {
            return (((v - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
        }
        /// <summary>Maps a decimal from one numeric interval to another.</summary>
        public static decimal RemapRanges(this decimal v, decimal oldMin, decimal oldMax, decimal newMin, decimal newMax)
        {
            return (((v - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
        }
        /// <summary>Maps a long from one numeric interval to another using integer arithmetic.</summary>
        public static long RemapRanges(this long v, long oldMin, long oldMax, long newMin, long newMax)
        {
            return (((v - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
        }
        /// <summary>Maps an int through the float overload and truncates the result to int.</summary>
        public static int RemapRanges(this int v, int oldMin, int oldMax, int newMin, int newMax)
        {
            return (int)RemapRanges((float)v, (float)oldMin, (float)oldMax, (float)newMin, (float)newMax);
        }

        /// <summary>Short character ramp used for density display.</summary>
        public static string StrDensityBlocks = " .,:;-+E#@";
        /// <summary>Long character ramp used for density display.</summary>
        public static string StrDensityText = ".'`^\",:;Il!i><~+_-?[]{}1()\\|/tfrxnuvcxzYXUJCLQ0OZmwqpdkbhao*#MW&8%B@$";
        /// <summary>Maps a clamped byte-range value through the block ramp using the supplied gradient length.</summary>
        public static char ByteToDensity(int b, string gradient) => StrDensityBlocks[((int)b.Clamp(0, 255)).RemapRanges(0,256,0,gradient.Length-1)];
        /// <summary>Maps a clamped byte-range value through the long text ramp.</summary>
        public static char ByteToDensityText(int b) => StrDensityText[((int)b.Clamp(0, 255)).RemapRanges(0,256,0, StrDensityText.Length)];
        /// <summary>Maps a clamped byte-range value through the short block ramp.</summary>
        public static char ByteToDensityBlocks(int b) => StrDensityBlocks[((int)b.Clamp(0, 255)).RemapRanges(0,256,0, StrDensityBlocks.Length)];
    
        /// <summary>Limits an integer to the inclusive min-to-max interval.</summary>
        public static int Clamp(this int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
