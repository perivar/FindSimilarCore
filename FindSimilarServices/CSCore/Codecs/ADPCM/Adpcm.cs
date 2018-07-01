using System;
using System.IO;
using System.Text;
using CSCore;
using CommonUtils.Audio;

namespace FindSimilarServices.CSCore.Codecs.ADPCM
{
    public class Adpcm
    {

        /*****************************************************************************
         * adpcm.c : adpcm variant audio decoder
         *****************************************************************************
         * Copyright (C) 2001, 2002 VLC authors and VideoLAN
         * $Id$
         *
         * Authors: Laurent Aimar <fenrir@via.ecp.fr>
         *          Rémi Denis-Courmont <rem # videolan.org>
         *
         * This program is free software; you can redistribute it and/or modify it
         * under the terms of the GNU Lesser General Public License as published by
         * the Free Software Foundation; either version 2.1 of the License, or
         * (at your option) any later version.
         *
         * This program is distributed in the hope that it will be useful,
         * but WITHOUT ANY WARRANTY; without even the implied warranty of
         * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
         * GNU Lesser General Public License for more details.
         *
         * You should have received a copy of the GNU Lesser General Public License
         * along with this program; if not, write to the Free Software Foundation,
         * Inc., 51 Franklin Street, Fifth Floor, Boston MA 02110-1301, USA.
         *****************************************************************************/

        /*****************************************************************************
         * Preamble
         *
         * Documentation: http://www.pcisys.net/~melanson/codecs/adpcm.txt
         *****************************************************************************/

        /*****************************************************************************
         * Local prototypes
         *****************************************************************************/
        public enum AdpcmCodecType
        {
            ADPCM_IMA_QT,
            ADPCM_IMA_WAV,
            ADPCM_MS,
            ADPCM_DK3,
            ADPCM_DK4,
            ADPCM_EA
        };

        public struct Decoder
        {
            public DecoderSys Sys;
            public AudioFormat AudioFormat;
            public bool Success;
        }

        public struct DecoderSys
        {
            public AdpcmCodecType Codec;

            public int BlockAlign;
            public int SamplesPerBlock;

            public short[] Prev;
        };


        /* Various table from http://www.pcisys.net/~melanson/codecs/adpcm.txt */
        static readonly int[] IndexTable =
        {
            -1, -1, -1, -1, 2, 4, 6, 8,
            -1, -1, -1, -1, 2, 4, 6, 8
        };

        static readonly int[] StepTable =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17,
            19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
            50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
            130, 143, 157, 173, 190, 209, 230, 253, 279, 307,
            337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
            876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066,
            2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358,
            5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
            15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
        };

        static readonly int[] AdaptationTable =
        {
            230, 230, 230, 230, 307, 409, 512, 614,
            768, 614, 512, 409, 307, 230, 230, 230
        };

        static readonly int[] AdaptationCoeff1 =
        {
            256, 512, 0, 192, 240, 460, 392
        };

        static readonly int[] AdaptationCoeff2 =
        {
            0, -256, 0, 64, 0, -208, -232
        };

        /*****************************************************************************
         * OpenDecoder: probe the decoder and return score
         *****************************************************************************/
        public static Decoder OpenDecoder(Decoder decoder)
        {
            var format = decoder.AudioFormat;

            var sys = new DecoderSys();
            sys.Prev = null;
            sys.SamplesPerBlock = 0;
            sys.Codec = AdpcmCodecType.ADPCM_MS;

            switch (format.Encoding)
            {
                // case VLC_CODEC_ADPCM_IMA_QT:
                case AudioEncoding.ImaAdpcm:
                case AudioEncoding.Adpcm:
                    // case VLC_CODEC_ADPCM_DK4:
                    // case VLC_CODEC_ADPCM_DK3:
                    // case VLC_CODEC_ADPCM_XA_EA:
                    break;
                default:
                    decoder.Success = false;
                    return decoder;
            }

            if (format.SampleRate <= 0)
            {
                Console.Error.WriteLine("Bad samplerate {0}", format.SampleRate);
                decoder.Success = false;
                return decoder;
            }

            int channels = format.Channels;
            byte maxChannels = 5;
            switch (format.Encoding)
            {
                //  case VLC_CODEC_ADPCM_IMA_QT: /* IMA ADPCM */
                // sys.codec = adpcmCodecE.ADPCM_IMA_QT;
                // iMaxChannels = 2;
                // break;
                case AudioEncoding.ImaAdpcm: /* IMA ADPCM */
                    sys.Codec = AdpcmCodecType.ADPCM_IMA_WAV;
                    maxChannels = 2;
                    break;
                case AudioEncoding.Adpcm: /* MS ADPCM */
                    sys.Codec = AdpcmCodecType.ADPCM_MS;
                    maxChannels = 2;
                    break;
                    // case VLC_CODEC_ADPCM_DK4: /* Duck DK4 ADPCM */
                    // sys.codec = adpcmCodecE.ADPCM_DK4;
                    // iMaxChannels = 2;
                    // break;
                    // case VLC_CODEC_ADPCM_DK3: /* Duck DK3 ADPCM */
                    // sys.codec = adpcmCodecE.ADPCM_DK3;
                    // iMaxChannels = 2;
                    // break;
                    // case VLC_CODEC_ADPCM_XA_EA: /* EA ADPCM */
                    // sys.codec = adpcmCodecE.ADPCM_EA;
                    // break;
            }

            if (channels > maxChannels || channels == 0)
            {
                Console.Error.WriteLine("Invalid number of channels {0}", channels);
                decoder.Success = false;
                return decoder;
            }

            if (format.BlockAlign <= 0)
            {
                sys.BlockAlign = (sys.Codec == AdpcmCodecType.ADPCM_IMA_QT) ? 34 * channels : 1024;
                Debug.WriteLine("Warning: block size undefined, using {0}", sys.BlockAlign);
            }
            else
            {
                sys.BlockAlign = format.BlockAlign;
            }

            /* calculate samples per block */
            switch (sys.Codec)
            {
                case AdpcmCodecType.ADPCM_IMA_QT:
                    sys.SamplesPerBlock = 64;
                    break;
                case AdpcmCodecType.ADPCM_IMA_WAV:
                    if (sys.BlockAlign >= 4 * channels)
                    {
                        sys.SamplesPerBlock =
                            2 * (sys.BlockAlign - 4 * channels) / channels;
                    }
                    break;
                case AdpcmCodecType.ADPCM_MS:
                    if (sys.BlockAlign >= 7 * channels)
                    {
                        sys.SamplesPerBlock =
                            2 * (sys.BlockAlign - 7 * channels) / channels + 2;
                    }
                    break;
                case AdpcmCodecType.ADPCM_DK4:
                    if (sys.BlockAlign >= 4 * channels)
                    {
                        sys.SamplesPerBlock =
                            2 * (sys.BlockAlign - 4 * channels) / channels + 1;
                    }
                    break;
                case AdpcmCodecType.ADPCM_DK3:
                    channels = 2;
                    if (sys.BlockAlign >= 16)
                    {
                        sys.SamplesPerBlock = (4 * (sys.BlockAlign - 16) + 2) / 3;
                    }
                    break;
                case AdpcmCodecType.ADPCM_EA:
                    if (sys.BlockAlign >= channels)
                    {
                        sys.SamplesPerBlock =
                            2 * (sys.BlockAlign - channels) / channels;
                    }
                    break;
            }

            Debug.WriteLine("Format: samplerate: {0}Hz, channels: {1}, bits/sample: {2}, blockalign: {3}, samplesperblock: {4}",
            format.SampleRate, format.Channels, format.BitsPerSample, sys.BlockAlign, sys.SamplesPerBlock);

            if (sys.SamplesPerBlock == 0)
            {
                Console.Error.WriteLine("Error computing number of samples per block");
                decoder.Success = false;
                return decoder;
            }

            decoder.Sys = sys;
            decoder.Success = true;
            return decoder;
        }

        /*****************************************************************************
         * DecodeBlock:
         *****************************************************************************/
        public static byte[] DecodeBlock(Decoder decoder, BinaryReader binaryReader)
        {
            DecoderSys sys = decoder.Sys;
            byte[] buffer = null;

            switch (sys.Codec)
            {
                case AdpcmCodecType.ADPCM_IMA_QT:
                    buffer = DecodeAdpcmImaQT(decoder, binaryReader);
                    break;
                case AdpcmCodecType.ADPCM_IMA_WAV:
                    buffer = DecodeAdpcmImaWav(decoder, binaryReader);
                    break;
                case AdpcmCodecType.ADPCM_MS:
                    buffer = DecodeAdpcmMs(decoder, binaryReader);
                    break;
                case AdpcmCodecType.ADPCM_DK4:
                    buffer = DecodeAdpcmDk4(decoder, binaryReader);
                    break;
                case AdpcmCodecType.ADPCM_DK3:
                    buffer = DecodeAdpcmDk3(decoder, binaryReader);
                    break;
                case AdpcmCodecType.ADPCM_EA:
                    DecodeAdpcmEA(decoder, binaryReader);
                    break;
                default:
                    break;
            }

            return buffer;
        }

        public static byte[] DecodeAudio(Decoder decoder, BinaryReader binaryReader, int count)
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            byte[] buffer = null;
            while ((buffer = DecodeBlock(decoder, binaryReader)) != null)
            {
                pcmOut.Write(buffer);
            }

            // We're done writing PCM data
            pcmOut.Close();
            output.Close();

            // Return the array.
            return output.ToArray();
        }

        /*
         * MS
         */
        public struct adpcmMsChannel
        {
            public short Delta;
            public short Sample1, Sample2;
            public short Coeff1, Coeff2;

        };

        static short AdpcmMsExpandNibble(adpcmMsChannel channel,
                                       int nibble)
        {
            // Get a signed number out of the nibble. We need to retain the
            // original nibble value for when we access AdaptionTable[].
            sbyte signedNibble = (sbyte)nibble;
            if ((signedNibble & 0x8) == 0x8)
            {
                signedNibble -= 0x10;
            }

            // Calculate new sample
            int predictor = (channel.Sample1 * channel.Coeff1 +
                            channel.Sample2 * channel.Coeff2) / 256 +
                          signedNibble * channel.Delta;

            // Clamp result to 16-bit, -32768 - 32767
            predictor = Clamp(predictor, short.MinValue, short.MaxValue);

            // Shuffle samples, get new delta
            channel.Sample2 = channel.Sample1;
            channel.Sample1 = (short)predictor;

            channel.Delta = (short)((AdaptationTable[nibble] * channel.Delta) / 256);

            // Saturate the delta to a lower bound of 16
            if (channel.Delta < 16)
            {
                channel.Delta = 16;
            }
            return (short)predictor;
        }

        static byte[] DecodeAdpcmMs(Decoder decoder, BinaryReader binaryReader)
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            DecoderSys sys = decoder.Sys;
            adpcmMsChannel[] channel = new adpcmMsChannel[2];

            int totalSamples = sys.SamplesPerBlock;
            if (totalSamples < 2)
                return null;

            bool isStereo = decoder.AudioFormat.Channels == 2 ? true : false;

            byte blockPredictor = 0;
            blockPredictor = binaryReader.ReadByte();
            blockPredictor = (byte)Clamp(blockPredictor, 0, 6);

            channel[0].Coeff1 = (short)AdaptationCoeff1[blockPredictor];
            channel[0].Coeff2 = (short)AdaptationCoeff2[blockPredictor];

            if (isStereo)
            {
                blockPredictor = binaryReader.ReadByte();
                blockPredictor = (byte)Clamp(blockPredictor, 0, 6);
                channel[1].Coeff1 = (short)AdaptationCoeff1[blockPredictor];
                channel[1].Coeff2 = (short)AdaptationCoeff2[blockPredictor];
            }
            channel[0].Delta = binaryReader.ReadInt16();
            if (isStereo)
            {
                channel[1].Delta = binaryReader.ReadInt16();
            }

            channel[0].Sample1 = binaryReader.ReadInt16();
            if (isStereo)
            {
                channel[1].Sample1 = binaryReader.ReadInt16();
            }

            channel[0].Sample2 = binaryReader.ReadInt16();
            if (isStereo)
            {
                channel[1].Sample2 = binaryReader.ReadInt16();
            }

            // output the samples
            if (isStereo)
            {
                pcmOut.Write(channel[0].Sample2);
                pcmOut.Write(channel[1].Sample2);
                pcmOut.Write(channel[0].Sample1);
                pcmOut.Write(channel[1].Sample1);
            }
            else
            {
                pcmOut.Write(channel[0].Sample2);
                pcmOut.Write(channel[0].Sample1);
            }

            for (totalSamples -= 2; totalSamples >= 2; totalSamples -= 2)
            {
                byte buffer = binaryReader.ReadByte();
                pcmOut.Write(AdpcmMsExpandNibble(channel[0], (buffer) >> 4));  //top four bits
                pcmOut.Write(AdpcmMsExpandNibble(channel[isStereo ? 1 : 0], (buffer) & 0x0f)); //bottom four bits
            }

            // We're done writing PCM data
            pcmOut.Close();
            output.Close();

            // Return the array.
            return output.ToArray();
        }

        /*
         * IMA-WAV
         */
        public struct AdpcmImaWavChannel
        {
            public int Predictor;
            public int StepIndex;

        };

        static int AdpcmImaWavExpandNibble(AdpcmImaWavChannel channel,
                                           int nibble)
        {
            /* Step 4 - Compute difference and new predicted value */
            /*
             ** Computes 'vpdiff = (delta+0.5)*step/4', but see comment
             ** in adpcm_coder.
             */
            int diff = StepTable[channel.StepIndex] >> 3;
            if ((nibble & 0x04) != 0) diff += StepTable[channel.StepIndex];
            if ((nibble & 0x02) != 0) diff += StepTable[channel.StepIndex] >> 1;
            if ((nibble & 0x01) != 0) diff += StepTable[channel.StepIndex] >> 2;
            if ((nibble & 0x08) != 0)
                channel.Predictor -= diff;
            else
                channel.Predictor += diff;


            /* Step 5 - clamp output value */
            channel.Predictor = Clamp(channel.Predictor, -32768, 32767);

            /* Step 6 - Update step value */
            channel.StepIndex += IndexTable[nibble];

            channel.StepIndex = Clamp(channel.StepIndex, 0, 88);

            return channel.Predictor;
        }

        static byte[] DecodeAdpcmImaWav(Decoder decoder, BinaryReader binaryReader)
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            DecoderSys sys = decoder.Sys;
            AdpcmImaWavChannel[] channel = new AdpcmImaWavChannel[2];
            int nibbles;
            short[] sample = new short[1000];
            bool isStereo = decoder.AudioFormat.Channels == 2 ? true : false;

            channel[0].Predictor = binaryReader.ReadInt16();
            channel[0].StepIndex = binaryReader.ReadByte();
            Clamp(channel[0].StepIndex, 0, 88);
            binaryReader.ReadByte();

            if (isStereo)
            {
                channel[1].Predictor = binaryReader.ReadInt16();
                channel[1].StepIndex = binaryReader.ReadByte();
                Clamp(channel[1].StepIndex, 0, 88);
                binaryReader.ReadByte();
            }

            if (isStereo)
            {
                for (nibbles = 2 * (sys.BlockAlign - 8); nibbles > 0; nibbles -= 16)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        byte buffer = binaryReader.ReadByte();
                        sample[i * 4] = (short)AdpcmImaWavExpandNibble(channel[0], buffer & 0x0f);
                        sample[i * 4 + 2] = (short)AdpcmImaWavExpandNibble(channel[0], buffer >> 4);
                    }
                    binaryReader.ReadInt32();

                    for (int i = 0; i < 4; i++)
                    {
                        byte buffer = binaryReader.ReadByte();
                        sample[i * 4 + 1] = (short)AdpcmImaWavExpandNibble(channel[1], buffer & 0x0f);
                        sample[i * 4 + 3] = (short)AdpcmImaWavExpandNibble(channel[1], buffer >> 4);
                    }
                    binaryReader.ReadInt32();
                    // sample += 16;
                }
            }
            else
            {
                for (nibbles = 2 * (sys.BlockAlign - 4); nibbles > 0; nibbles -= 2)
                {
                    byte buffer = binaryReader.ReadByte();
                    pcmOut.Write(AdpcmImaWavExpandNibble(channel[0], (buffer) & 0x0f));
                    pcmOut.Write(AdpcmImaWavExpandNibble(channel[0], (buffer) >> 4));
                }
            }

            // We're done writing PCM data
            pcmOut.Close();
            output.Close();

            // Return the array.
            return output.ToArray();
        }

        /*
         * Ima4 in QT file
         */
        static byte[] DecodeAdpcmImaQT(Decoder decoder, BinaryReader binaryReader)
        {
            AdpcmImaWavChannel[] channel = new AdpcmImaWavChannel[2];
            int nibbles;

            byte[] buffer = new byte[1000];
            short[] sample = new short[1000];

            int channels = decoder.AudioFormat.Channels;

            for (int iCh = 0; iCh < channels; iCh++)
            {
                /* load preamble */
                channel[iCh].Predictor = (short)((((buffer[0] << 1) | (buffer[1] >> 7))) << 7);
                channel[iCh].StepIndex = buffer[1] & 0x7f;

                Clamp(channel[iCh].StepIndex, 0, 88);
                // buffer += 2;

                for (nibbles = 0; nibbles < 64; nibbles += 2)
                {
                    // sample = AdpcmImaWavExpandNibble(channel[iCh], (buffer) & 0x0f);
                    // sample += step;

                    // sample = AdpcmImaWavExpandNibble(channel[iCh], (buffer >> 4) & 0x0f);
                    // sample += step;

                    binaryReader.ReadByte();
                }

                /* Next channel */
                // sample += 1 - 64 * step;
            }

            return null;
        }

        /*
         * Dk4
         */
        static byte[] DecodeAdpcmDk4(Decoder decoder, BinaryReader binaryReader)
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            DecoderSys sys = decoder.Sys;
            AdpcmImaWavChannel[] channel = new AdpcmImaWavChannel[2];
            int nibbles;

            bool isStereo = decoder.AudioFormat.Channels == 2 ? true : false;

            channel[0].Predictor = binaryReader.ReadInt16();
            channel[0].StepIndex = binaryReader.ReadByte();
            Clamp(channel[0].StepIndex, 0, 88);
            binaryReader.ReadByte();

            if (isStereo)
            {
                channel[1].Predictor = binaryReader.ReadInt16();
                channel[1].StepIndex = binaryReader.ReadByte();
                Clamp(channel[1].StepIndex, 0, 88);
                binaryReader.ReadByte();
            }

            /* first output predictor */
            pcmOut.Write(channel[0].Predictor);
            if (isStereo)
            {
                pcmOut.Write(channel[1].Predictor);
            }

            for (nibbles = 0;
                 nibbles < sys.BlockAlign - 4 * (isStereo ? 2 : 1);
                 nibbles++)
            {
                byte buffer = binaryReader.ReadByte();
                pcmOut.Write(AdpcmImaWavExpandNibble(channel[0], (buffer) >> 4));
                pcmOut.Write(AdpcmImaWavExpandNibble(channel[isStereo ? 1 : 0], (buffer) & 0x0f));

                binaryReader.ReadByte();
            }

            // We're done writing PCM data
            pcmOut.Close();
            output.Close();

            // Return the array.
            return output.ToArray();
        }

        /*
         * Dk3
         */
        static byte[] DecodeAdpcmDk3(Decoder decoder, BinaryReader binaryReader)
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            DecoderSys sys = decoder.Sys;
            byte[] buffer = new byte[1000];
            byte pEnd = buffer[sys.BlockAlign];
            AdpcmImaWavChannel sum;
            AdpcmImaWavChannel diff;
            int iDiffValue;

            // buffer += 10;

            sum.Predictor = binaryReader.ReadInt16();
            diff.Predictor = binaryReader.ReadInt16();
            sum.StepIndex = binaryReader.ReadByte();
            diff.StepIndex = binaryReader.ReadByte();

            iDiffValue = diff.Predictor;
            /* we process 6 nibbles at once */
            for (int i = 0; i < pEnd; i++)
            {
                byte buff = buffer[i];
                /* first 3 nibbles */
                AdpcmImaWavExpandNibble(sum, (buff) & 0x0f);

                AdpcmImaWavExpandNibble(diff, (buff) >> 4);

                iDiffValue = (iDiffValue + diff.Predictor) / 2;

                pcmOut.Write(sum.Predictor + iDiffValue);
                pcmOut.Write(sum.Predictor - iDiffValue);

                binaryReader.ReadByte();

                AdpcmImaWavExpandNibble(sum, (buff) & 0x0f);

                pcmOut.Write(sum.Predictor + iDiffValue);
                pcmOut.Write(sum.Predictor - iDiffValue);

                /* now last 3 nibbles */
                AdpcmImaWavExpandNibble(sum, (buff) >> 4);
                binaryReader.ReadByte();
                if (i < pEnd)
                {
                    AdpcmImaWavExpandNibble(diff, (buff) & 0x0f);

                    iDiffValue = (iDiffValue + diff.Predictor) / 2;

                    pcmOut.Write(sum.Predictor + iDiffValue);
                    pcmOut.Write(sum.Predictor - iDiffValue);

                    AdpcmImaWavExpandNibble(sum, (buff) >> 4);
                    binaryReader.ReadByte();

                    pcmOut.Write(sum.Predictor + iDiffValue);
                    pcmOut.Write(sum.Predictor - iDiffValue);
                }
            }

            // We're done writing PCM data
            pcmOut.Close();
            output.Close();

            // Return the array.
            return output.ToArray();
        }


        public struct AdpcmEASpl
        {
            public UInt32 u; // unsigned
            public Int32 i; // signed
        }

        /*
         * EA ADPCM
         */
        static byte[] DecodeAdpcmEA(Decoder decoder, BinaryReader binaryReader)
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            int[] EATable =
            {
                0x0000, 0x00F0, 0x01CC, 0x0188, 0x0000, 0x0000, 0xFF30, 0xFF24,
                0x0000, 0x0001, 0x0003, 0x0004, 0x0007, 0x0008, 0x000A, 0x000B,
                0x0000, 0xFFFF, 0xFFFD, 0xFFFC
            };

            DecoderSys sys = decoder.Sys;
            byte[] buffer = new byte[1000];
            int[] c1 = new int[2];
            int[] c2 = new int[2];
            int[] d = new int[2];

            int channels = decoder.AudioFormat.Channels;
            short[] prev = sys.Prev;
            int[] cur = new int[prev.Length + channels];

            for (int c = 0; c < channels; c++)
            {
                byte input = buffer[c];

                c1[c] = EATable[input >> 4];
                c2[c] = EATable[(input >> 4) + 4];
                d[c] = (input & 0xf) + 8;
            }

            for (int i = 0; i < sys.BlockAlign; i += channels)
            {
                AdpcmEASpl spl = new AdpcmEASpl();

                for (int c = 0; c < channels; c++)
                {
                    spl.u = (uint)((buffer[c] & 0xf0u) << 24);
                    spl.i >>= d[c];
                    spl.i = (spl.i + cur[c] * c1[c] + prev[c] * c2[c] + 0x80) >> 8;

                    Clamp(spl.i, -32768, 32767);
                    prev[c] = (short)cur[c];
                    cur[c] = spl.i;

                    pcmOut.Write(spl.i);
                }

                for (int c = 0; c < channels; c++)
                {
                    spl.u = (uint)(buffer[c] & 0x0fu) << 28;
                    spl.i >>= d[c];
                    spl.i = (spl.i + cur[c] * c1[c] + prev[c] * c2[c] + 0x80) >> 8;

                    Clamp(spl.i, -32768, 32767);
                    prev[c] = (short)cur[c];
                    cur[c] = spl.i;

                    pcmOut.Write(spl.i);
                }
            }

            // We're done writing PCM data
            pcmOut.Close();
            output.Close();

            // Return the array.
            return output.ToArray();
        }

        // util to clamp a number within a given range
        private static int Clamp(int num, int min, int max)
        {
            return num <= min ? min : num >= max ? max : num;
        }
    }
}