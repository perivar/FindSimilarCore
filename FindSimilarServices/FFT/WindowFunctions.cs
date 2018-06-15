using System;

namespace FindSimilarServices.FFT
{
    public interface IWindowFunction
    {
        void Initialize(int winsize);

        void Apply(ref float[] data, float[] audiodata, int offset);

        double[] GetWindow();
    }

    public class HammingWindow : IWindowFunction
    {
        int winsize;
        double[] win;

        public HammingWindow()
        {
        }

        // Initialize and setup the window
        public HammingWindow(int winsize)
        {
            Initialize(winsize);
        }

        public double[] GetWindow()
        {
            return win;
        }

        public void Initialize(int winsize)
        {
            this.winsize = winsize;
            win = new double[winsize];

            for (int i = 0; i < winsize; i++)
            {
                win[i] = (double)(0.54 - 0.46 * Math.Cos(2 * Math.PI * ((double)i / (double)winsize)));
            }
        }

        public void Apply(ref float[] data, float[] audiodata, int offset)
        {
            for (int i = 0; i < winsize; i++)
            {
                data[i] = (float)win[i] * audiodata[i + offset];
            }
        }
    }

    public class HannWindow : IWindowFunction
    {
        int winsize;
        double[] win;

        public HannWindow()
        {
        }

        // Initialize and setup the window
        public HannWindow(int winsize)
        {
            Initialize(winsize);
        }

        public double[] GetWindow()
        {
            return win;
        }

        public void Initialize(int winsize)
        {
            this.winsize = winsize;
            win = new double[winsize];

            for (int i = 0; i < winsize; i++)
            {
                win[i] = (double)(0.5 * (1 - Math.Cos(2 * Math.PI * (double)i / (winsize - 1))));
            }
        }

        public void Apply(ref float[] data, float[] audiodata, int offset)
        {
            for (int i = 0; i < winsize; i++)
            {
                data[i] = (float)win[i] * audiodata[i + offset];
            }
        }
    }
}

