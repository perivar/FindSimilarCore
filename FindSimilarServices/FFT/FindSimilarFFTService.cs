using SoundFingerprinting.FFT;

namespace FindSimilarServices.FFT
{
    public class FindSimilarFFTService : IFFTService
    {
        Lomont.LomontFFT lomontFFT;

        public FindSimilarFFTService(int wdftSize) {
            lomontFFT = new Lomont.LomontFFT();
            lomontFFT.A = 1;
            lomontFFT.B = 1;
            lomontFFT.Initialize(wdftSize);
            
        }

        public float[] FFTForward(float[] data, int startIndex, int length, float[] window)
        {
            var toTransform = new double[length];
            for (int i = startIndex, j = 0; i < startIndex + length; ++i, ++j)
            {
                toTransform[j] = data[i];
            }

            // perform windowing
            for (int i = 0; i < window.Length; ++i)
            {
                toTransform[i] = toTransform[i] * window[i];
            }

            lomontFFT.RealFFT(toTransform, true);

            float[] transformed = new float[length];
            for (int i = 0; i < length; ++i)
            {
                transformed[i] = (float) toTransform[i];
            }

            return transformed;
        }
    }
}