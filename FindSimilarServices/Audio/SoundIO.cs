using System;

namespace CommonUtils.Audio
{
    public static class SoundIO
    {
        // compression code, 2 bytes
        public const int WAVE_FORMAT_UNKNOWN = 0x0000; // Microsoft Corporation
        public const int WAVE_FORMAT_PCM = 0x0001; // Microsoft Corporation
        public const int WAVE_FORMAT_ADPCM = 0x0002; // Microsoft Corporation
        public const int WAVE_FORMAT_IEEE_FLOAT = 0x0003; // Microsoft Corporation
        public const int WAVE_FORMAT_ALAW = 0x0006; // Microsoft Corporation
        public const int WAVE_FORMAT_MULAW = 0x0007; // Microsoft Corporation
        public const int WAVE_FORMAT_DTS_MS = 0x0008; // Microsoft Corporation
        public const int WAVE_FORMAT_WMAS = 0x000a; // WMA 9 Speech
        public const int WAVE_FORMAT_IMA_ADPCM = 0x0011; // Intel Corporation
        public const int WAVE_FORMAT_TRUESPEECH = 0x0022; // TrueSpeech
        public const int WAVE_FORMAT_GSM610 = 0x0031; // Microsoft Corporation
        public const int WAVE_FORMAT_MSNAUDIO = 0x0032; // Microsoft Corporation
        public const int WAVE_FORMAT_G726 = 0x0045; // ITU-T standard
        public const int WAVE_FORMAT_MPEG = 0x0050; // Microsoft Corporation
        public const int WAVE_FORMAT_MPEGLAYER3 = 0x0055; // ISO/MPEG Layer3 Format Tag
        public const int WAVE_FORMAT_DOLBY_AC3_SPDIF = 0x0092; // Sonic Foundry
        public const int WAVE_FORMAT_A52 = 0x2000;
        public const int WAVE_FORMAT_DTS = 0x2001;
        public const int WAVE_FORMAT_WMA1 = 0x0160; // WMA version 1
        public const int WAVE_FORMAT_WMA2 = 0x0161; // WMA (v2) 7, 8, 9 Series
        public const int WAVE_FORMAT_WMAP = 0x0162; // WMA 9 Professional
        public const int WAVE_FORMAT_WMAL = 0x0163; // WMA 9 Lossless
        public const int WAVE_FORMAT_DIVIO_AAC = 0x4143;
        public const int WAVE_FORMAT_AAC = 0x00FF;
        public const int WAVE_FORMAT_FFMPEG_AAC = 0x706D;

        public const int WAVE_FORMAT_DK3 = 0x0061;
        public const int WAVE_FORMAT_DK4 = 0x0062;
        public const int WAVE_FORMAT_VORBIS = 0x566f;
        public const int WAVE_FORMAT_VORB_1 = 0x674f;
        public const int WAVE_FORMAT_VORB_2 = 0x6750;
        public const int WAVE_FORMAT_VORB_3 = 0x6751;
        public const int WAVE_FORMAT_VORB_1PLUS = 0x676f;
        public const int WAVE_FORMAT_VORB_2PLUS = 0x6770;
        public const int WAVE_FORMAT_VORB_3PLUS = 0x6771;
        public const int WAVE_FORMAT_SPEEX = 0xa109; // Speex audio
        public const int WAVE_FORMAT_EXTENSIBLE = 0xFFFE; // Microsoft

        public static string[] INFO_TYPE = { "IARL", "IART", "ICMS", "ICMT", "ICOP",
            "ICRD", "ICRP", "IDIM", "IDPI", "IENG", "IGNR", "IKEY",
            "ILGT", "IMED", "INAM", "IPLT", "IPRD", "ISBJ",
            "ISFT", "ISHP", "ISRC", "ISRF", "ITCH",
            "ISMP", "IDIT", "VXNG", "TURL" };

        public static string[] INFO_DESC = { "Archival location", "Artist", "Commissioned", "Comments", "Copyright",
            "Creation date", "Cropped", "Dimensions", "Dots per inch", "Engineer", "Genre", "Keywords",
            "Lightness settings", "Medium", "Name of subject", "Palette settings", "Product", "Description",
            "Software package", "Sharpness", "Source", "Source form", "Digitizing technician",
            "SMPTE time code", "Digitization time", "VXNG", "Url" };

        /*
         * Native Formats
         * Number of Bits	MATLAB Data Type			Data Range
         * 8				uint8 (unsigned integer) 	0 <= y <= 255
         * 16				int16 (signed integer) 		-32768 <= y <= +32767
         * 24				int32 (signed integer) 		-2^23 <= y <= 2^23-1
         * 32				single (floating point) 	-1.0 <= y < +1.0
         * 
         * typedef uint8_t  u8_t;     ///< unsigned 8-bit value (0 to 255)
         * typedef int8_t   s8_t;     ///< signed 8-bit value (-128 to +127)
         * typedef uint16_t u16_t;    ///< unsigned 16-bit value (0 to 65535)
         * typedef int16_t  s16_t;    ///< signed 16-bit value (-32768 to 32767)
         * typedef uint32_t u32_t;    ///< unsigned 32-bit value (0 to 4294967296)
         * typedef int32_t  s32_t;    ///< signed 32-bit value (-2147483648 to +2147483647)
         */

        public static void Read8Bit(BinaryFile waveFile, float[][] sound, int sampleCount, int channels)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                for (int ic = 0; ic < channels; ic++)
                {
                    byte b = waveFile.ReadByte();
                    sound[ic][i] = (float)b / 128.0f - 1.0f;
                }
            }
        }

        public static void Write8Bit(BinaryFile waveFile, float[][] sound, int sampleCount, int channels)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                for (int ic = 0; ic < channels; ic++)
                {
                    int val = SoundIOUtils.RoundToClosestInt((sound[ic][i] + 1) * 128);

                    if (val > 255)
                        val = 255;
                    if (val < 0)
                        val = 0;

                    byte b = (byte)val;

                    waveFile.Write(b);
                }
            }
        }
        public static void Read16Bit(BinaryFile waveFile, float[][] sound, int sampleCount, int channels)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                for (int ic = 0; ic < channels; ic++)
                {
                    float f = (float)waveFile.ReadInt16();
                    f = f / 32768.0f;
                    sound[ic][i] = f;
                }
            }
        }

        public static void Write16Bit(BinaryFile waveFile, float[][] sound, int sampleCount, int channels)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                for (int ic = 0; ic < channels; ic++)
                {
                    int val = SoundIOUtils.RoundToClosestInt(sound[ic][i] * 32768);

                    if (val > 32767)
                        val = 32767;
                    if (val < -32768)
                        val = -32768;

                    waveFile.Write((Int16)val);
                }
            }
        }

        public static void Read32Bit(BinaryFile waveFile, float[][] sound, int sampleCount, int channels)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                for (int ic = 0; ic < channels; ic++)
                {
                    float f = (float)waveFile.ReadInt32();
                    f = f / 2147483648.0f;
                    sound[ic][i] = f;
                }
            }
        }

        public static void Write32Bit(BinaryFile waveFile, float[][] sound, int sampleCount, int channels)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                for (int ic = 0; ic < channels; ic++)
                {
                    int val = SoundIOUtils.RoundToClosestInt(sound[ic][i] * 2147483648);

                    if (val > 2147483647)
                        val = 2147483647;
                    if (val < -2147483648)
                        val = -2147483648;

                    waveFile.Write((int)val);
                }
            }
        }

        public static void Read32BitFloat(BinaryFile waveFile, float[][] sound, int sampleCount, int channels)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                for (int ic = 0; ic < channels; ic++)
                {
                    float d = waveFile.ReadSingle();
                    sound[ic][i] = d;
                }
            }
        }

        public static void Write32BitFloat(BinaryFile waveFile, float[][] sound, int sampleCount, int channels)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                for (int ic = 0; ic < channels; ic++)
                {
                    waveFile.Write(sound[ic][i]);
                }
            }
        }

        public static float[][] ReadWaveFile(BinaryFile waveFile, ref int channels, ref int sampleCount, ref int sampleRate)
        {
            float[][] sound;
            var tag = new int[13];

            // integers
            int RIFF = BinaryFile.StringToInt32("RIFF");    // 1179011410
            int WAVE = BinaryFile.StringToInt32("WAVE");    // 1163280727
            int FMT = BinaryFile.StringToInt32("fmt ");     // 544501094
            int DATA = BinaryFile.StringToInt32("data");    // 1635017060

            //			Size  Description                  Value
            // tag[0]	4	  RIFF Header				   RIFF (1179011410)
            // tag[1] 	4	  RIFF data size
            // tag[2] 	4	  form-type (WAVE etc)			(1163280727)
            // tag[3] 	4     Chunk ID                     "fmt " (0x666D7420) = 544501094
            // tag[4]	4     Chunk Data Size              16 + extra format bytes 	// long chunkSize;
            // tag[5]	2     Compression code             1 - 65,535	// short wFormatTag;
            // tag[6]	2     Number of channels           1 - 65,535
            // tag[7]	4     Sample rate                  1 - 0xFFFFFFFF
            // tag[8]	4     Average bytes per second     1 - 0xFFFFFFFF
            // tag[9]	2     Block align                  1 - 65,535 (4)
            // tag[10]	2     Significant bits per sample  2 - 65,535 (32)
            // tag[11]	4	  IEEE = 1952670054 (0x74636166) = fact chunk
            // 				  PCM = 1635017060 (0x61746164)  (datachunk = 1635017060)
            // tag[12] 	4	  IEEE = 4 , 						PCM = 5292000 (0x0050BFE0)

            // tag reading
            for (int i = 0; i < 13; i++)
            {
                tag[i] = 0;

                if ((i == 5) || (i == 6) || (i == 9) || (i == 10))
                {
                    tag[i] = waveFile.ReadUInt16();
                }
                else
                {
                    tag[i] = (int)waveFile.ReadUInt32();
                }
            }

            #region File format checking
            if (tag[0] != RIFF || tag[2] != WAVE)
            {
                throw new FormatException("This file is not in WAVE format");
            }

            // fmt tag, chunkSize and data tag
            if (tag[3] != FMT || tag[4] != 16 || tag[11] != DATA)
            {
                throw new NotSupportedException("This WAVE file format is not currently supported");
            }

            // bits per sample
            if (tag[10] == 24)
            {
                throw new NotSupportedException("24 bit PCM WAVE files are not currently supported");
            }

            // wFormatTag
            if (tag[5] != WAVE_FORMAT_PCM && tag[5] != WAVE_FORMAT_IEEE_FLOAT)
            {
                throw new NotSupportedException("Non PCM WAVE files are not currently supported");
            }
            #endregion File format checking

            channels = tag[6];
            sampleCount = tag[12] / (tag[10] / 8) / channels;
            sampleRate = tag[7];

            sound = new float[channels][];

            for (int ic = 0; ic < channels; ic++)
            {
                sound[ic] = new float[sampleCount];
            }

            #region Data loading
            if (tag[10] == 8)
            {
                Read8Bit(waveFile, sound, sampleCount, channels);
            }
            if (tag[10] == 16)
            {
                Read16Bit(waveFile, sound, sampleCount, channels);
            }
            if (tag[10] == 32)
            {
                if (tag[5] == WAVE_FORMAT_PCM)
                {
                    Read32Bit(waveFile, sound, sampleCount, channels);
                }
                else if (tag[5] == WAVE_FORMAT_IEEE_FLOAT)
                {
                    Read32BitFloat(waveFile, sound, sampleCount, channels);
                }
            }
            #endregion Data loading

            waveFile.Close();
            return sound;
        }

        public static void WriteWaveFile(string path, float[][] sound, int channels, int sampleRate, int bitsPerSample = 32)
        {
            WriteWaveFile(new BinaryFile(path, BinaryFile.ByteOrder.LittleEndian, true), sound, channels, sound[0].Length, sampleRate, bitsPerSample);
        }

        public static void WriteWaveFile(string path, float[] monosound, int sampleRate, int bitsPerSample = 32)
        {
            WriteWaveFile(new BinaryFile(path, BinaryFile.ByteOrder.LittleEndian, true), new float[][] { monosound }, 1, monosound.Length, sampleRate, bitsPerSample);
        }

        public static void WriteWaveFile(BinaryFile waveFile, float[][] sound, int channels, int sampleCount, int sampleRate, int bitsPerSample = 32)
        {
            /*
            Offset   Size  Description                  Value
            0x00     4     Chunk ID                     "fmt " (0x666D7420)
            0x04     4     Chunk Data Size              16 + extra format bytes
            0x08     2     Compression code             1 - 65,535
            0x0a     2     Number of channels           1 - 65,535
            0x0c     4     Sample rate                  1 - 0xFFFFFFFF
            0x10     4     Average bytes per second     1 - 0xFFFFFFFF
            0x14     2     Block align                  1 - 65,535
            0x16     2     Significant bits per sample  2 - 65,535
            0x18     2     Extra format bytes           0 - 65,535
            */

            #region WAV tags generation
            // integers
            int RIFF = BinaryFile.StringToInt32("RIFF");    // 1179011410
            int WAVE = BinaryFile.StringToInt32("WAVE");    // 1163280727
            int FMT = BinaryFile.StringToInt32("fmt ");     // 544501094
            int DATA = BinaryFile.StringToInt32("data");    // 1635017060

            int[] tag = { RIFF, 0, WAVE, FMT, 16, 1, 1, 0, 0, 0, 0, DATA, 0, 0 };

            tag[12] = sampleCount * (bitsPerSample / 8) * channels;
            tag[1] = tag[12] + 36;

            if ((bitsPerSample == 8) || (bitsPerSample == 16))
                tag[5] = WAVE_FORMAT_PCM;

            if (bitsPerSample == 32)
                tag[5] = WAVE_FORMAT_IEEE_FLOAT;

            tag[6] = channels;
            tag[7] = sampleRate;
            tag[8] = sampleRate * bitsPerSample / 8; // Average bytes per second
            tag[9] = bitsPerSample / 8; // Block align
            tag[10] = bitsPerSample; // Significant bits per sample

            #endregion WAV tags generation

            // tag writing
            for (int i = 0; i < 13; i++)
            {
                if ((i == 5) || (i == 6) || (i == 9) || (i == 10))
                {
                    waveFile.Write((ushort)tag[i]);
                }
                else
                {
                    waveFile.Write((uint)tag[i]);
                }
            }

            if (bitsPerSample == 8)
                Write8Bit(waveFile, sound, sampleCount, channels);
            if (bitsPerSample == 16)
                Write16Bit(waveFile, sound, sampleCount, channels);
            if (bitsPerSample == 32)
                Write32BitFloat(waveFile, sound, sampleCount, channels);

            waveFile.Close();
        }
    }
}
