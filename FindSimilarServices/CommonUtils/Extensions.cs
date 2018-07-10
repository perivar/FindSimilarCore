using System;
using System.IO;

namespace CommonUtils
{
    public static class ExtensionsMethods
    {
        #region memory stream extensions
        public static void Clear(this MemoryStream stream)
        {
            stream.SetLength(0);
        }

        public static long Remaining(this MemoryStream stream)
        {
            return stream.Length - stream.Position;
        }
        #endregion

        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }
    }
}