using System;

namespace CommonUtils.Audio
{
    public static class SoundIOUtils
    {
        #region Rounding
        public static int RoundToClosestInt(double x)
        {
            int y = 0;
            // use AwayFromZero since default rounding is "round to even", which would make 1.5 => 1
            y = (int)Math.Round(x, MidpointRounding.AwayFromZero);

            // nearbyint: The value of x rounded to a nearby integral (as a floating-point value).
            // Rounding using to-nearest rounding:
            // nearbyint (2.3) = 2.0
            // nearbyint (3.8) = 4.0
            // nearbyint (-2.3) = -2.0
            // nearbyint (-3.8) = -4.0
            return y;
        }

        public static int RoundUpToClosestInt(double x)
        {
            int y = 0;
            y = (int)MathUtils.RoundUp(x);
            return y;
        }
        #endregion
    }
}