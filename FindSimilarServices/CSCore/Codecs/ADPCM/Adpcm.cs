using System;
using System.IO;
using System.Text;
using CSCore;

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
        public enum adpcmCodec
        {
            ADPCM_IMA_QT,
            ADPCM_IMA_WAV,
            ADPCM_MS,
            ADPCM_DK3,
            ADPCM_DK4,
            ADPCM_EA
        };

        public struct decoder
        {
            public decoderSys Sys;
            public WaveFormat WaveFormat;
        }

        public struct decoderSys
        {
            public adpcmCodec codec;

            public int block;
            public int samplesPerBlock;

            public DateTime endDate;
            public short[] prev;
        };


        /* Various table from http://www.pcisys.net/~melanson/codecs/adpcm.txt */
        static readonly int[] indexTable =
        {
            -1, -1, -1, -1, 2, 4, 6, 8,
            -1, -1, -1, -1, 2, 4, 6, 8
        };

        static readonly int[] stepTable =
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

        static readonly int[] adaptationTable =
        {
            230, 230, 230, 230, 307, 409, 512, 614,
            768, 614, 512, 409, 307, 230, 230, 230
        };

        static readonly int[] adaptationCoeff1 =
        {
            256, 512, 0, 192, 240, 460, 392
        };

        static readonly int[] adaptationCoeff2 =
        {
            0, -256, 0, 64, 0, -208, -232
        };

        /*****************************************************************************
         * OpenDecoder: probe the decoder and return score
         *****************************************************************************/
        static bool OpenDecoder(decoder pDec, WaveFormat format)
        {
            decoderSys pSys = new decoderSys();

            switch (format.WaveFormatTag)
            {
                // case VLC_CODEC_ADPCM_IMA_QT:
                case AudioEncoding.ImaAdpcm:
                case AudioEncoding.Adpcm:
                    // case VLC_CODEC_ADPCM_DK4:
                    // case VLC_CODEC_ADPCM_DK3:
                    // case VLC_CODEC_ADPCM_XA_EA:
                    break;
                default:
                    return false;
            }

            if (format.SampleRate <= 0)
            {
                // not supported: msg_Err( pDec, "bad samplerate" );
                return false;
            }

            pSys.prev = null;
            pSys.samplesPerBlock = 0;
            pSys.codec = adpcmCodec.ADPCM_MS;

            int iChannels = format.Channels;
            byte iMaxChannels = 5;
            switch (format.WaveFormatTag)
            {
                //  case VLC_CODEC_ADPCM_IMA_QT: /* IMA ADPCM */
                // pSys.codec = adpcmCodecE.ADPCM_IMA_QT;
                // iMaxChannels = 2;
                // break;
                case AudioEncoding.ImaAdpcm: /* IMA ADPCM */
                    pSys.codec = adpcmCodec.ADPCM_IMA_WAV;
                    iMaxChannels = 2;
                    break;
                case AudioEncoding.Adpcm: /* MS ADPCM */
                    pSys.codec = adpcmCodec.ADPCM_MS;
                    iMaxChannels = 2;
                    break;
                    // case VLC_CODEC_ADPCM_DK4: /* Duck DK4 ADPCM */
                    // pSys.codec = adpcmCodecE.ADPCM_DK4;
                    // iMaxChannels = 2;
                    // break;
                    // case VLC_CODEC_ADPCM_DK3: /* Duck DK3 ADPCM */
                    // pSys.codec = adpcmCodecE.ADPCM_DK3;
                    // iMaxChannels = 2;
                    // break;
                    // case VLC_CODEC_ADPCM_XA_EA: /* EA ADPCM */
                    // pSys.codec = adpcmCodecE.ADPCM_EA;
                    // break;
            }

            if (iChannels > iMaxChannels || iChannels == 0)
            {
                // not supported: free(pSys[prev)];
                // not supported: free(pSys);
                // not supported: msg_Err( pDec, "Invalid number of channels %i", pDec[fmtIn].audio.iChannels );
                return false;
            }

            if (format.BlockAlign <= 0)
            {
                pSys.block = (pSys.codec == adpcmCodec.ADPCM_IMA_QT) ? 34 * iChannels : 1024;
                // not supported: msg_Warn( pDec, "block size undefined, using %zu", block );
            }
            else
            {
                pSys.block = format.BlockAlign;
            }

            /* calculate samples per block */
            switch (pSys.codec)
            {
                case adpcmCodec.ADPCM_IMA_QT:
                    pSys.samplesPerBlock = 64;
                    break;
                case adpcmCodec.ADPCM_IMA_WAV:
                    if (pSys.block >= 4 * iChannels)
                    {
                        pSys.samplesPerBlock =
                            2 * (pSys.block - 4 * iChannels) / iChannels;
                    }
                    break;
                case adpcmCodec.ADPCM_MS:
                    if (pSys.block >= 7 * iChannels)
                    {
                        pSys.samplesPerBlock =
                            2 * (pSys.block - 7 * iChannels) / iChannels + 2;
                    }
                    break;
                case adpcmCodec.ADPCM_DK4:
                    if (pSys.block >= 4 * iChannels)
                    {
                        pSys.samplesPerBlock =
                            2 * (pSys.block - 4 * iChannels) / iChannels + 1;
                    }
                    break;
                case adpcmCodec.ADPCM_DK3:
                    iChannels = 2;
                    if (pSys.block >= 16)
                    {
                        pSys.samplesPerBlock = (4 * (pSys.block - 16) + 2) / 3;
                    }
                    break;
                case adpcmCodec.ADPCM_EA:
                    if (pSys.block >= iChannels)
                    {
                        pSys.samplesPerBlock =
                            2 * (pSys.block - iChannels) / iChannels;
                    }
                    break;
            }

            /*             msg_Dbg(pDec, "format: samplerate:%d Hz channels:%d bits/sample:%d "
                        "blockalign:%zu samplesperblock:%zu",
                         pDec[fmtIn].audio.iRate, iChannels,
                         pDec[fmtIn].audio.iBitspersample, block,
                         samplesPerBlock);
             */
             
            if (pSys.samplesPerBlock == 0)
            {
                // not supported: free(pSys[prev)];
                // not supported: free(pSys);
                // not supported: msg_Err( pDec, "Error computing number of samples per block");
                return false;
            }

            pDec.Sys = pSys;
            /*             pDec.fmtOut.iCodec = VLC_CODEC_S16N;
                        pDec.fmtOut.audio.iRate = pDec.fmtIn.audio.iRate;
                        pDec.waveFormat.Channels = iChannels;
                        pDec.fmtOut.audio.iPhysicalChannels = vlcChanMaps[iChannels];

                        date_Init(pSys.endDate, pDec.fmtOut.audio.iRate, 1);

                        pDec.pfDecode = DecodeAudio;
                        pDec.pfFlush = Flush;
             */

            return true;
        }

        /*****************************************************************************
         * DecodeBlock:
         *****************************************************************************/
        static byte[] DecodeBlock(decoder pDec, byte[] ppBlock)
        {
            decoderSys pSys = pDec.Sys;
            byte[] pOutBuffer = new byte[1000];
            BinaryReader binaryReader = null;

            switch (pSys.codec)
            {
                case adpcmCodec.ADPCM_IMA_QT:
                    pOutBuffer = DecodeAdpcmImaQT(pDec, binaryReader);
                    break;
                case adpcmCodec.ADPCM_IMA_WAV:
                    pOutBuffer = DecodeAdpcmImaWav(pDec, binaryReader);
                    break;
                case adpcmCodec.ADPCM_MS:
                    pOutBuffer = DecodeAdpcmMs(pDec, binaryReader);
                    break;
                case adpcmCodec.ADPCM_DK4:
                    pOutBuffer = DecodeAdpcmDk4(pDec, binaryReader);
                    break;
                case adpcmCodec.ADPCM_DK3:
                    pOutBuffer = DecodeAdpcmDk3(pDec, binaryReader);
                    break;
                case adpcmCodec.ADPCM_EA:
                    DecodeAdpcmEA(pDec, binaryReader);
                    break;
                default:
                    break;
            }

            return pOutBuffer;
        }

        /*
         * MS
         */
        public struct adpcmMsChannelT
        {
            public int delta;
            public int sample1, sample2;
            public int coeff1, coeff2;

        };

        static int AdpcmMsExpandNibble(adpcmMsChannelT channel,
                                       int nibble)
        {
            int predictor;

            // Get a signed number out of the nibble. We need to retain the
            // original nibble value for when we access AdaptionTable[].
            sbyte signedNibble = (sbyte)nibble;
            if ((signedNibble & 0x8) == 0x8)
            {
                signedNibble -= 0x10;
            }

            // Calculate new sample
            predictor = (channel.sample1 * channel.coeff1 +
                            channel.sample2 * channel.coeff2) / 256 +
                          signedNibble * channel.delta;

            // Clamp result to 32-bit
            predictor = CLAMP(predictor, -32768, 32767);

            // Shuffle samples, get new delta
            channel.sample2 = channel.sample1;
            channel.sample1 = predictor;

            channel.delta = (adaptationTable[nibble] *
                                    channel.delta) / 256;

            // Saturate the delta to a lower bound of 16
            if (channel.delta < 16)
            {
                channel.delta = 16;
            }
            return predictor;
        }

        static byte[] DecodeAdpcmMs(decoder pDec, BinaryReader binaryReader)
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            decoderSys pSys = pDec.Sys;
            adpcmMsChannelT[] channel = new adpcmMsChannelT[2];

            int totalSamples = pSys.samplesPerBlock;
            if (totalSamples < 2)
                return null;

            bool bStereo = pDec.WaveFormat.Channels == 2 ? true : false;

            byte blockPredictor = 0;
            blockPredictor = binaryReader.ReadByte();
            blockPredictor = (byte)CLAMP(blockPredictor, 0, 6);

            channel[0].coeff1 = adaptationCoeff1[blockPredictor];
            channel[0].coeff2 = adaptationCoeff2[blockPredictor];

            if (bStereo)
            {
                blockPredictor = binaryReader.ReadByte();
                blockPredictor = (byte)CLAMP(blockPredictor, 0, 6);
                channel[1].coeff1 = adaptationCoeff1[blockPredictor];
                channel[1].coeff2 = adaptationCoeff2[blockPredictor];
            }
            channel[0].delta = binaryReader.ReadInt16();
            if (bStereo)
            {
                channel[1].delta = binaryReader.ReadInt16();
            }

            channel[0].sample1 = binaryReader.ReadInt16();
            if (bStereo)
            {
                channel[1].sample1 = binaryReader.ReadInt16();
            }

            channel[0].sample2 = binaryReader.ReadInt16();
            if (bStereo)
            {
                channel[1].sample2 = binaryReader.ReadInt16();
            }

            // output the samples
            if (bStereo)
            {
                pcmOut.Write(channel[0].sample2);
                pcmOut.Write(channel[1].sample2);
                pcmOut.Write(channel[0].sample1);
                pcmOut.Write(channel[1].sample1);
            }
            else
            {
                pcmOut.Write(channel[0].sample2);
                pcmOut.Write(channel[0].sample1);
            }

            for (totalSamples -= 2; totalSamples >= 2; totalSamples -= 2)
            {
                byte buffer = binaryReader.ReadByte();
                pcmOut.Write(AdpcmMsExpandNibble(channel[0], (buffer) >> 4));  //top four bits
                pcmOut.Write(AdpcmMsExpandNibble(channel[bStereo ? 1 : 0], (buffer) & 0x0f)); //bottom four bits
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
        public struct adpcmImaWavChannelT
        {
            public int predictor;
            public int stepIndex;

        };

        static int AdpcmImaWavExpandNibble(adpcmImaWavChannelT pChannel,
                                           int nibble)
        {
            /* Step 4 - Compute difference and new predicted value */
            /*
             ** Computes 'vpdiff = (delta+0.5)*step/4', but see comment
             ** in adpcm_coder.
             */
            int diff = stepTable[pChannel.stepIndex] >> 3;
            if ((nibble & 0x04) != 0) diff += stepTable[pChannel.stepIndex];
            if ((nibble & 0x02) != 0) diff += stepTable[pChannel.stepIndex] >> 1;
            if ((nibble & 0x01) != 0) diff += stepTable[pChannel.stepIndex] >> 2;
            if ((nibble & 0x08) != 0)
                pChannel.predictor -= diff;
            else
                pChannel.predictor += diff;


            /* Step 5 - clamp output value */
            pChannel.predictor = CLAMP(pChannel.predictor, -32768, 32767);

            /* Step 6 - Update step value */
            pChannel.stepIndex += indexTable[nibble];

            pChannel.stepIndex = CLAMP(pChannel.stepIndex, 0, 88);

            return pChannel.predictor;
        }

        static byte[] DecodeAdpcmImaWav(decoder pDec, BinaryReader binaryReader)
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            decoderSys pSys = pDec.Sys;
            adpcmImaWavChannelT[] channel = new adpcmImaWavChannelT[2];
            int nibbles;
            short[] sample = new short[1000];
            bool bStereo = pDec.WaveFormat.Channels == 2 ? true : false;

            channel[0].predictor = binaryReader.ReadInt16();
            channel[0].stepIndex = binaryReader.ReadByte();
            CLAMP(channel[0].stepIndex, 0, 88);
            binaryReader.ReadByte();

            if (bStereo)
            {
                channel[1].predictor = binaryReader.ReadInt16();
                channel[1].stepIndex = binaryReader.ReadByte();
                CLAMP(channel[1].stepIndex, 0, 88);
                binaryReader.ReadByte();
            }

            if (bStereo)
            {
                for (nibbles = 2 * (pSys.block - 8); nibbles > 0; nibbles -= 16)
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
                for (nibbles = 2 * (pSys.block - 4); nibbles > 0; nibbles -= 2)
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
        static byte[] DecodeAdpcmImaQT(decoder pDec, BinaryReader binaryReader)
        {
            adpcmImaWavChannelT[] channel = new adpcmImaWavChannelT[2];
            int nibbles;

            byte[] buffer = new byte[1000];
            short[] sample = new short[1000];

            int step = pDec.WaveFormat.Channels;

            for (int iCh = 0; iCh < step; iCh++)
            {
                /* load preambule */
                channel[iCh].predictor = (short)((((buffer[0] << 1) | (buffer[1] >> 7))) << 7);
                channel[iCh].stepIndex = buffer[1] & 0x7f;

                CLAMP(channel[iCh].stepIndex, 0, 88);
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
        static byte[] DecodeAdpcmDk4(decoder pDec, BinaryReader binaryReader)
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            decoderSys pSys = pDec.Sys;
            adpcmImaWavChannelT[] channel = new adpcmImaWavChannelT[2];
            int nibbles;

            bool bStereo = pDec.WaveFormat.Channels == 2 ? true : false;

            channel[0].predictor = binaryReader.ReadInt16();
            channel[0].stepIndex = binaryReader.ReadByte();
            CLAMP(channel[0].stepIndex, 0, 88);
            binaryReader.ReadByte();

            if (bStereo)
            {
                channel[1].predictor = binaryReader.ReadInt16();
                channel[1].stepIndex = binaryReader.ReadByte();
                CLAMP(channel[1].stepIndex, 0, 88);
                binaryReader.ReadByte();
            }

            /* first output predictor */
            pcmOut.Write(channel[0].predictor);
            if (bStereo)
            {
                pcmOut.Write(channel[1].predictor);
            }

            for (nibbles = 0;
                 nibbles < pSys.block - 4 * (bStereo ? 2 : 1);
                 nibbles++)
            {
                byte buffer = binaryReader.ReadByte();
                pcmOut.Write(AdpcmImaWavExpandNibble(channel[0], (buffer) >> 4));
                pcmOut.Write(AdpcmImaWavExpandNibble(channel[bStereo ? 1 : 0], (buffer) & 0x0f));

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
        static byte[] DecodeAdpcmDk3(decoder pDec, BinaryReader binaryReader)
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            decoderSys pSys = pDec.Sys;
            byte[] buffer = new byte[1000];
            byte pEnd = buffer[pSys.block];
            adpcmImaWavChannelT sum;
            adpcmImaWavChannelT diff;
            int iDiffValue;

            // buffer += 10;

            sum.predictor = binaryReader.ReadInt16();
            diff.predictor = binaryReader.ReadInt16();
            sum.stepIndex = binaryReader.ReadByte();
            diff.stepIndex = binaryReader.ReadByte();

            iDiffValue = diff.predictor;
            /* we process 6 nibbles at once */
            for (int i = 0; i < pEnd; i++)
            {
                byte buff = buffer[i];
                /* first 3 nibbles */
                AdpcmImaWavExpandNibble(sum, (buff) & 0x0f);

                AdpcmImaWavExpandNibble(diff, (buff) >> 4);

                iDiffValue = (iDiffValue + diff.predictor) / 2;

                pcmOut.Write(sum.predictor + iDiffValue);
                pcmOut.Write(sum.predictor - iDiffValue);

                binaryReader.ReadByte();

                AdpcmImaWavExpandNibble(sum, (buff) & 0x0f);

                pcmOut.Write(sum.predictor + iDiffValue);
                pcmOut.Write(sum.predictor - iDiffValue);

                /* now last 3 nibbles */
                AdpcmImaWavExpandNibble(sum, (buff) >> 4);
                binaryReader.ReadByte();
                if (i < pEnd)
                {
                    AdpcmImaWavExpandNibble(diff, (buff) & 0x0f);

                    iDiffValue = (iDiffValue + diff.predictor) / 2;

                    pcmOut.Write(sum.predictor + iDiffValue);
                    pcmOut.Write(sum.predictor - iDiffValue);

                    AdpcmImaWavExpandNibble(sum, (buff) >> 4);
                    binaryReader.ReadByte();

                    pcmOut.Write(sum.predictor + iDiffValue);
                    pcmOut.Write(sum.predictor - iDiffValue);
                }
            }

            // We're done writing PCM data
            pcmOut.Close();
            output.Close();

            // Return the array.
            return output.ToArray();
        }

        /*
         * EA ADPCM
         */
        static byte[] DecodeAdpcmEA(decoder pDec, BinaryReader binaryReader)
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

            /*             decoderSysT pSys = pDec.pSys;
                        intFast32T c1[MAX_CHAN], c2[MAX_CHAN];
                        intFast8T d[MAX_CHAN];

                        int chans = pDec.waveFormat.Channels;
                        const byte pEnd = buffer[pSys.block];
                        short prev = pSys.prev;
                        short cur = prev + chans;

                        for (unsigned c = 0; c < chans; c++)
                        {
                            byte input = buffer[c];

                            c1[c] = EATable[input >> 4];
                            c2[c] = EATable[(input >> 4) + 4];
                            d[c] = (input & 0xf) + 8;
                        }

                        for (buffer += chans; buffer < pEnd; buffer += chans)
                        {
                            union { uint u; int i; }
                            spl;

                            for (unsigned c = 0; c < chans; c++)
                            {
                                spl.u = (buffer[c] & 0xf0u) << 24u;
                                spl.i >>= d[c];
                                spl.i = (spl.i + cur[c] * c1[c] + prev[c] * c2[c] + 0x80) >> 8;
                                CLAMP(spl.i, -32768, 32767);
                                prev[c] = cur[c];
                                cur[c] = spl.i;

                                *(sample++) = spl.i;
                            }

                            for (unsigned c = 0; c < chans; c++)
                            {
                                spl.u = (buffer[c] & 0x0fu) << 28u;
                                spl.i >>= d[c];
                                spl.i = (spl.i + cur[c] * c1[c] + prev[c] * c2[c] + 0x80) >> 8;
                                CLAMP(spl.i, -32768, 32767);
                                prev[c] = cur[c];
                                cur[c] = spl.i;

                                *(sample++) = spl.i;
                            }
                        }
            */

            // We're done writing PCM data
            pcmOut.Close();
            output.Close();

            // Return the array.
            return output.ToArray();
        }

        // util to clamp a number within a given range
        private static int CLAMP(int num, int min, int max)
        {
            return num <= min ? min : num >= max ? max : num;
        }
    }
}