using System.Globalization;
using System.IO;

namespace FindSimilarServices
{
    public static class WriteOutputUtils
    {
        /// <summary>Writes the float array to an ascii-textfile that can be read by Matlab.
        /// Usage in Matlab: load('filename', '-ascii');</summary>
        /// <param name="data">data</param>
        /// <param name="filename">the name of the ascii file to create, e.g. "C:\\temp\\data.ascii"</param>
        public static void WriteAscii(float[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write(" {0}\r", data[i].ToString("#.00000000e+000", CultureInfo.InvariantCulture));
            }
            pw.Close();
        }

        /// <summary>Writes the double array to an ascii-textfile that can be read by Matlab.
        /// Usage in Matlab: load('filename', '-ascii');</summary>
        /// <param name="data">data</param>
        /// <param name="filename">the name of the ascii file to create, e.g. "C:\\temp\\data.ascii"</param>
        public static void WriteAscii(double[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write(" {0}\r", data[i].ToString("#.00000000e+000", CultureInfo.InvariantCulture));
            }
            pw.Close();
        }

        /// <summary>Writes the short array to an ascii-textfile that can be read by Matlab.
        /// Usage in Matlab: load('filename', '-ascii');</summary>
        /// <param name="data">data</param>
        /// <param name="filename">the name of the ascii file to create, e.g. "C:\\temp\\data.ascii"</param>
        public static void WriteAscii(short[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write(" {0}\r", data[i].ToString("#.00000000e+000", CultureInfo.InvariantCulture));
            }
            pw.Close();
        }

        /// <summary>
        /// Write matrix to file using F3 formatting
        /// </summary>
        /// <param name="data">data</param>
        /// <param name="filename">filename</param>
        public static void WriteF3Formatted(float[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write("{0}", data[i].ToString("F3", CultureInfo.InvariantCulture).PadLeft(10) + " ");
                pw.Write("\r");
            }
            pw.Close();
        }

        /// <summary>
        /// Write matrix to file using F3 formatting
        /// </summary>
        /// <param name="data">data</param>
        /// <param name="filename">filename</param>
        public static void WriteF3Formatted(double[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write("{0}", data[i].ToString("F3", CultureInfo.InvariantCulture).PadLeft(10) + " ");
                pw.Write("\r");
            }
            pw.Close();
        }

        /// <summary>
        /// Write matrix to file using F3 formatting
        /// </summary>
        /// <param name="data">data</param>
        /// <param name="filename">filename</param>
        public static void WriteF3Formatted(short[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write("{0}", data[i].ToString("F3", CultureInfo.InvariantCulture).PadLeft(10) + " ");
                pw.Write("\r");
            }
            pw.Close();
        }
    }
}