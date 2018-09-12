using System;
using System.IO;

namespace CSCore.Codecs.AIFF
{
    internal class AiffBinaryReader
    {
        private readonly BinaryReader _binaryReader;

        public AiffBinaryReader(BinaryReader binaryReader)
        {
            if (binaryReader == null)
                throw new ArgumentNullException("binaryReader");
            _binaryReader = binaryReader;
        }

        public double ReadIeeeExtended()
        {
            return ConvertFromIeeeExtended(ReadBytes(10));
        }

        public int ReadInt32()
        {
            var buffer = ReadBytes(4);
            return ((buffer[0] << 24) |
                    (buffer[1] << 16) |
                    (buffer[2] << 8) |
                    buffer[3]);
        }

        public uint ReadUInt32()
        {
            var buffer = ReadBytes(4);
            return (uint)((buffer[0] << 24) |
                           (buffer[1] << 16) |
                           (buffer[2] << 8) |
                           buffer[3]);
        }

        public short ReadInt16()
        {
            var buffer = ReadBytes(2);
            return (short)((buffer[0] << 8) |
                            (buffer[1]));
        }

        public ushort ReadUInt16()
        {
            var buffer = ReadBytes(2);
            return (ushort)((buffer[0] << 8) |
                             (buffer[1]));
        }

        public void Skip(long count)
        {
            if (_binaryReader.BaseStream.CanSeek)
                _binaryReader.BaseStream.Seek(count, SeekOrigin.Current);
            else
                _binaryReader.ReadBytes((int)count);
        }

        private byte[] ReadBytes(int count)
        {
            var bytes = _binaryReader.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new EndOfStreamException(string.Format("Could not read {0} bytes. Only {1} bytes were read.",
                    count, bytes.Length));
            }
            return bytes;
        }

        //copied from https://github.com/naudio/NAudio/blob/master/NAudio/Utils/IEEE.cs
        #region ConvertFromIeeeExtended
        /// <summary>
        /// Converts an IEEE 80-bit extended precision number to a
        /// C# double precision number.
        /// </summary>
        /// <param name="bytes">The 80-bit IEEE extended number (as an array of 10 bytes).</param>
        /// <returns>A C# double precision number that is a close representation of the IEEE extended number.</returns>
        private double ConvertFromIeeeExtended(byte[] bytes)
        {
            if (bytes.Length != 10) throw new Exception("Incorrect length for IEEE extended.");
            double f;
            int expon;
            uint hiMant, loMant;

            expon = ((bytes[0] & 0x7F) << 8) | bytes[1];
            hiMant = (uint)((bytes[2] << 24) | (bytes[3] << 16) | (bytes[4] << 8) | bytes[5]);
            loMant = (uint)((bytes[6] << 24) | (bytes[7] << 16) | (bytes[8] << 8) | bytes[9]);

            if (expon == 0 && hiMant == 0 && loMant == 0)
            {
                f = 0;
            }
            else
            {
                if (expon == 0x7FFF)    /* Infinity or NaN */
                {
                    f = double.NaN;
                }
                else
                {
                    expon -= 16383;
                    f = ldexp(UnsignedToFloat(hiMant), expon -= 31);
                    f += ldexp(UnsignedToFloat(loMant), expon -= 32);
                }
            }

            if ((bytes[0] & 0x80) == 0x80) return -f;
            else return f;
        }
        #endregion

        private double ldexp(double x, int exp)
        {
            return x * Math.Pow(2, exp);
        }

        private double UnsignedToFloat(ulong u)
        {
            return (((double)((long)(u - 2147483647L - 1))) + 2147483648.0);
        }
    }
}