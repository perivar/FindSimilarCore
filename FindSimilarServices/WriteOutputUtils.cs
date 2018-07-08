using System;
using System.Collections.Generic;
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
        /// Write matrix to file using F6 formatting
        /// </summary>
        /// <param name="data">data</param>
        /// <param name="filename">filename</param>
        public static void WriteF6Formatted(float[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write("{0}", data[i].ToString("F6", CultureInfo.InvariantCulture).PadLeft(10) + " ");
                pw.Write("\r");
            }
            pw.Close();
        }

        /// <summary>
        /// Write matrix to file using F6 formatting
        /// </summary>
        /// <param name="data">data</param>
        /// <param name="filename">filename</param>
        public static void WriteF6Formatted(double[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write("{0}", data[i].ToString("F6", CultureInfo.InvariantCulture).PadLeft(10) + " ");
                pw.Write("\r");
            }
            pw.Close();
        }

        /// <summary>
        /// Write matrix to file using F6 formatting
        /// </summary>
        /// <param name="data">data</param>
        /// <param name="filename">filename</param>
        public static void WriteF6Formatted(short[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write("{0}", data[i].ToString("F6", CultureInfo.InvariantCulture).PadLeft(10) + " ");
                pw.Write("\r");
            }
            pw.Close();
        }

        /// <summary>
        /// Write matrix to file using CSV formatting
        /// </summary>
        /// <param name="data">data</param>
        /// <param name="filename">filename</param>
        public static void WriteCSV(float[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write("{0}", data[i].ToString("F6", CultureInfo.CurrentCulture));
                pw.Write("\r");
            }
            pw.Close();
        }

        /// <summary>
        /// Write matrix to file using CSV formatting
        /// </summary>
        /// <param name="data">data</param>
        /// <param name="filename">filename</param>
        public static void WriteCSV(double[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write("{0}", data[i].ToString("F6", CultureInfo.CurrentCulture));
                pw.Write("\r");
            }
            pw.Close();
        }

        /// <summary>
        /// Write matrix to file using CSV formatting
        /// </summary>
        /// <param name="data">data</param>
        /// <param name="filename">filename</param>
        public static void WriteCSV(short[] data, string filename)
        {
            TextWriter pw = File.CreateText(filename);
            for (int i = 0; i < data.Length; i++)
            {
                pw.Write("{0}", data[i].ToString("F6", CultureInfo.CurrentCulture));
                pw.Write("\r");
            }
            pw.Close();
        }

        /// <summary>
        /// Writes the Matrix to a comma separated file
        /// </summary>
        /// <param name="filename">the name of the csv file to create, e.g. "C:\\temp\\matrix.csv"</param>
        public static void WriteCSV(double[][] matrixData, string filename)
        {
            WriteCSV(matrixData, filename, ",");
        }

        /// <summary>
        /// Writes the Matrix to a comma separated file
        /// </summary>
        /// <param name="filename">the name of the csv file to create, e.g. "C:\\temp\\matrix.csv"</param>
        public static void WriteCSV(float[][] matrixData, string filename)
        {
            WriteCSV(matrixData, filename, ",");
        }

        /// <summary>
        /// Writes the Matrix to a text delimited file where the separator character can be specified
        /// </summary>
        /// <param name="filename">the name of the csv file to create, e.g. "C:\\temp\\matrix.csv"</param>
        /// <param name="columnSeparator">the separator character to use</param>
        public static void WriteCSV(double[][] matrixData, string filename, string columnSeparator)
        {
            TextWriter pw = File.CreateText(filename);
            int rowCount = matrixData.Length;
            int columnCount = matrixData[0].Length;
            for (int i = 0; i < columnCount; i++)
            {
                var columnElements = new List<string>();
                for (int j = 0; j < rowCount; j++)
                {
                    columnElements.Add(String.Format(CultureInfo.CurrentCulture, "\"{0:F6}\"", matrixData[j][i]));
                }
                pw.Write("{0}\r\n", string.Join(columnSeparator, columnElements));
            }
            pw.Close();
        }

        /// <summary>
        /// Writes the Matrix to a text delimited file where the separator character can be specified
        /// </summary>
        /// <param name="filename">the name of the csv file to create, e.g. "C:\\temp\\matrix.csv"</param>
        /// <param name="columnSeparator">the separator character to use</param>
        public static void WriteCSV(float[][] matrixData, string filename, string columnSeparator)
        {
            TextWriter pw = File.CreateText(filename);
            int rowCount = matrixData.Length;
            int columnCount = matrixData[0].Length;
            for (int i = 0; i < columnCount; i++)
            {
                var columnElements = new List<string>();
                for (int j = 0; j < rowCount; j++)
                {
                    columnElements.Add(String.Format(CultureInfo.CurrentCulture, "\"{0:F6}\"", matrixData[j][i]));
                }
                pw.Write("{0}\r\n", string.Join(columnSeparator, columnElements));
            }
            pw.Close();
        }
    }
}