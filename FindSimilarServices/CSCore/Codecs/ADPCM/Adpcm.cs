using System;
using System.IO;
using System.Text;
using CSCore;
using CommonUtils.Audio;
using System.Diagnostics;
using Serilog;

namespace CSCore.Codecs.ADPCM
{
    // Based on VLC ADPCM class
    // GNU Lesser General Public License
    // https://github.com/videolan/vlc/blob/master/modules/codec/adpcm.c
    public class Adpcm
    {
        // use sox to convert from wav to ADPCM:
        // sox snare.wav -e ms-adpcm snare-ms-adpcm.wav
        // sox snare.wav -e ima-adpcm snare-ima-adpcm.wav

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
            public DecoderState State;
            public AudioFormat AudioFormat;
        }

        public struct DecoderState
        {
            public AdpcmCodecType Codec;

            public int BlockAlign;
            public int SamplesPerBlock;

            public short[] Prev;
        };


        /* Various table from http://www.pcistate.net/~melanson/codecs/adpcm.txt */
        private static readonly int[] IMAIndexTable =
        {
            -1, -1, -1, -1, 2, 4, 6, 8,
            -1, -1, -1, -1, 2, 4, 6, 8
        };

        private static readonly int[] IMAStepTable =
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

        private static readonly int[] MSAdaptationTable =
        {
            230, 230, 230, 230, 307, 409, 512, 614,
            768, 614, 512, 409, 307, 230, 230, 230
        };

        private static readonly int[] MSAdaptationCoeff1 =
        {
            256, 512, 0, 192, 240, 460, 392
        };

        private static readonly int[] MSAdaptationCoeff2 =
        {
            0, -256, 0, 64, 0, -208, -232
        };

        public static readonly int[][] MSAdpcmICoeff = {
                        new int[] { 256,   0},
                        new int[] { 512,-256},
                        new int[] {   0,   0},
                        new int[] { 192,  64},
                        new int[] { 240,   0},
                        new int[] { 460,-208},
                        new int[] { 392,-232}
                        };

        /*****************************************************************************
         * OpenDecoder: probe the decoder and return score
         *****************************************************************************/
        public static bool OpenDecoder(ref Decoder decoder)
        {
            var format = decoder.AudioFormat;

            var state = new DecoderState();
            state.Prev = null;
            state.SamplesPerBlock = 0;
            state.Codec = AdpcmCodecType.ADPCM_MS;

            switch ((short)format.Encoding)
            {
                //case 0x00a4: // Apple QuickTime IMA ADPCM, FOURCCs: ima4
                case 0x0002: // Microsoft ADPCM
                case 0x0011: // IMA ADPCM   
                case 0x0061: // Duck DK4 IMA ADPCM
                case 0x0062: // Duck DK3 IMA ADPCM 
                             //case 0x0000: // EA ADPCM, XA ADPCM, FOURCCs: XAJ0
                    break;
                default:
                    return false;
            }

            if (format.SampleRate <= 0)
            {
                Console.Error.WriteLine("Bad samplerate {0}", format.SampleRate);
                return false;
            }

            int channels = format.Channels;
            byte maxChannels = 5;
            switch ((short)format.Encoding)
            {

                // case 0x00a4: // Apple QuickTime IMA ADPCM, FOURCCs: ima4
                // state.Codec = AdpcmCodecType.ADPCM_IMA_QT;
                // maxChannels = 2;
                // break;
                case 0x0002: // Microsoft ADPCM
                    state.Codec = AdpcmCodecType.ADPCM_MS;
                    maxChannels = 2;
                    break;
                case 0x0011: // IMA ADPCM
                    state.Codec = AdpcmCodecType.ADPCM_IMA_WAV;
                    maxChannels = 2;
                    break;
                case 0x0061: // Duck DK4 IMA ADPCM
                    state.Codec = AdpcmCodecType.ADPCM_DK4;
                    maxChannels = 2;
                    break;
                case 0x0062: // Duck DK3 IMA ADPCM 
                    state.Codec = AdpcmCodecType.ADPCM_DK3;
                    maxChannels = 2;
                    break;
                    // case 0x0000: // EA ADPCM, XA ADPCM, FOURCCs: XAJ0
                    // state.Codec = AdpcmCodecType.ADPCM_EA;
                    // break;
            }

            if (channels > maxChannels || channels == 0)
            {
                Console.Error.WriteLine("Invalid number of channels {0}", channels);
                return false;
            }

            if (format.BlockAlign <= 0)
            {
                state.BlockAlign = (state.Codec == AdpcmCodecType.ADPCM_IMA_QT) ? 34 * channels : 1024;
                Log.Verbose("Warning: block size undefined, using {0}", state.BlockAlign);
            }
            else
            {
                state.BlockAlign = format.BlockAlign;
            }

            // calculate samples per block
            switch (state.Codec)
            {
                case AdpcmCodecType.ADPCM_IMA_QT:
                    state.SamplesPerBlock = 64;
                    break;
                case AdpcmCodecType.ADPCM_IMA_WAV:
                    if (state.BlockAlign >= 4 * channels)
                    {
                        state.SamplesPerBlock =
                            2 * (state.BlockAlign - 4 * channels) / channels;
                    }
                    break;
                case AdpcmCodecType.ADPCM_MS:
                    if (state.BlockAlign >= 7 * channels)
                    {
                        state.SamplesPerBlock =
                            2 * (state.BlockAlign - 7 * channels) / channels + 2;
                    }
                    break;
                case AdpcmCodecType.ADPCM_DK4:
                    if (state.BlockAlign >= 4 * channels)
                    {
                        state.SamplesPerBlock =
                            2 * (state.BlockAlign - 4 * channels) / channels + 1;
                    }
                    break;
                case AdpcmCodecType.ADPCM_DK3:
                    channels = 2;
                    if (state.BlockAlign >= 16)
                    {
                        state.SamplesPerBlock = (4 * (state.BlockAlign - 16) + 2) / 3;
                    }
                    break;
                case AdpcmCodecType.ADPCM_EA:
                    if (state.BlockAlign >= channels)
                    {
                        state.SamplesPerBlock =
                            2 * (state.BlockAlign - channels) / channels;
                    }
                    break;
            }

            if (state.SamplesPerBlock == 0)
            {
                Console.Error.WriteLine("Error computing number of samples per block");
                return false;
            }

            Log.Verbose("Adpcm OpenDecoder: samplerate: {0}Hz, channels: {1}, bits/sample: {2}, blockAlign: {3}, samplesPerBlock: {4}", format.SampleRate, format.Channels, format.BitsPerSample, state.BlockAlign, state.SamplesPerBlock);

            decoder.State = state;
            return true;
        }

        /*****************************************************************************
         * DecodeBlock:
         *****************************************************************************/
        private static void DecodeBlock(Decoder decoder, BinaryReader reader, BinaryWriter writer)
        {
            DecoderState state = decoder.State;
            switch (state.Codec)
            {
                case AdpcmCodecType.ADPCM_IMA_QT:
                    DecodeAdpcmImaQT(decoder, reader, writer);
                    break;
                case AdpcmCodecType.ADPCM_IMA_WAV:
                    DecodeAdpcmImaWav(decoder, reader, writer);
                    break;
                case AdpcmCodecType.ADPCM_MS:
                    DecodeAdpcmMs(decoder, reader, writer);
                    break;
                case AdpcmCodecType.ADPCM_DK4:
                    DecodeAdpcmDk4(decoder, reader, writer);
                    break;
                case AdpcmCodecType.ADPCM_DK3:
                    DecodeAdpcmDk3(decoder, reader, writer);
                    break;
                case AdpcmCodecType.ADPCM_EA:
                    DecodeAdpcmEA(decoder, reader, writer);
                    break;
                default:
                    break;
            }
        }

        public static byte[] DecodeAudio(Decoder decoder, byte[] data, int bytesDataSize)
        {
            byte[] resultBytes;
            using (MemoryStream result = new MemoryStream())
            {
                BinaryReader reader = new BinaryReader(new MemoryStream(data, 0, bytesDataSize));
                BinaryWriter writer = new BinaryWriter(result);
                DecodeAllBlocks(decoder, bytesDataSize, reader, writer);
                resultBytes = result.ToArray();
            }

            return resultBytes;
        }

        private static void DecodeAllBlocks(Decoder decoder, int bytesDataSize, BinaryReader reader, BinaryWriter writer)
        {
            // determine total number of blocks
            double blocksFraction = (double)bytesDataSize / (double)decoder.AudioFormat.BytesPerBlock;
            int numberOfBlocks = (int)blocksFraction;

            for (int i = 0; i < numberOfBlocks; i++)
            {
                DecodeBlock(decoder, reader, writer);
            }
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

        private static short AdpcmMsExpandNibble(ref adpcmMsChannel channel, byte nibble)
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

            channel.Delta = (short)((MSAdaptationTable[nibble] * channel.Delta) / 256);

            // Saturate the delta to a lower bound of 16
            if (channel.Delta < 16)
            {
                channel.Delta = 16;
            }
            return (short)predictor;
        }

        private static void DecodeAdpcmMs(Decoder decoder, BinaryReader reader, BinaryWriter writer)
        {
            // https://wiki.multimedia.cx/index.php/Microsoft_ADPCM
            // see also https://github.com/DeltaEngine/DeltaEngine/blob/master/Multimedia/OpenAL/Helpers/MsAdpcmConverter.cs

            DecoderState state = decoder.State;
            adpcmMsChannel[] channel = new adpcmMsChannel[2];
            byte blockPredictor = 0;

            // determine total number of samples in this block
            // the initial 2 samples from the block preamble are sent directly to the output.
            // therefore, deduct 2 from the samples per block to calculate the remaining samples
            int totalSamples = (state.SamplesPerBlock - 2) * decoder.AudioFormat.Channels;
            if (totalSamples < 2)
                return;

            bool isStereo = decoder.AudioFormat.Channels == 2 ? true : false;

            //  read predicates and deltas
            blockPredictor = reader.ReadByte();
            blockPredictor = (byte)Clamp(blockPredictor, 0, 6);
            channel[0].Coeff1 = (short)MSAdaptationCoeff1[blockPredictor];
            channel[0].Coeff2 = (short)MSAdaptationCoeff2[blockPredictor];

            if (isStereo)
            {
                blockPredictor = reader.ReadByte();
                blockPredictor = (byte)Clamp(blockPredictor, 0, 6);
                channel[1].Coeff1 = (short)MSAdaptationCoeff1[blockPredictor];
                channel[1].Coeff2 = (short)MSAdaptationCoeff2[blockPredictor];
            }
            channel[0].Delta = reader.ReadInt16();
            if (isStereo)
            {
                channel[1].Delta = reader.ReadInt16();
            }

            //  read first samples and write them to result
            channel[0].Sample1 = reader.ReadInt16();
            if (isStereo)
            {
                channel[1].Sample1 = reader.ReadInt16();
            }

            channel[0].Sample2 = reader.ReadInt16();
            if (isStereo)
            {
                channel[1].Sample2 = reader.ReadInt16();
            }

            // output the samples
            if (isStereo)
            {
                writer.Write(channel[0].Sample2);
                writer.Write(channel[1].Sample2);
                writer.Write(channel[0].Sample1);
                writer.Write(channel[1].Sample1);
            }
            else
            {
                writer.Write(channel[0].Sample2);
                writer.Write(channel[0].Sample1);
            }

            // decode the rest of the samples
            for (int index = 0; index < totalSamples; index += 2)
            {
                byte nibble = reader.ReadByte();
                writer.Write(AdpcmMsExpandNibble(ref channel[0], (byte)(nibble >> 4)));
                writer.Write(AdpcmMsExpandNibble(ref channel[isStereo ? 1 : 0], (byte)(nibble & 0x0f)));
            }
        }

        /*
         * IMA-WAV
         */
        public struct AdpcmImaWavChannel
        {
            public int Predictor;
            public int StepIndex;

        };

        private static short AdpcmImaWavExpandNibble(ref AdpcmImaWavChannel channel, int nibble)
        {
            int step = IMAStepTable[channel.StepIndex];

            // perform direct multiplication instead of series of jumps proposed by
            // the reference ADPCM implementation since modern CPUs can do the mults
            // quickly enough
            int diff = ((((nibble & 7) << 1) + 1) * step) >> 3;

            if ((nibble & 8) != 0)
                diff = -diff;

            channel.Predictor = ((int)channel.Predictor) + diff;

            // Clamp result to 16-bit, -32768 - 32767
            channel.Predictor = Clamp(channel.Predictor, short.MinValue, short.MaxValue);

            channel.StepIndex = channel.StepIndex + IMAIndexTable[nibble];
            channel.StepIndex = Clamp(channel.StepIndex, 0, 88);

            return (short)channel.Predictor;
        }

        private static short AdpcmImaWavExpandNibbleOriginal(ref AdpcmImaWavChannel channel, int nibble)
        {
            // Compute difference and new predicted value
            // Computes 'vpdiff = (delta+0.5)*step/4', 
            // but see comment in adpcm_coder.             
            int diff = IMAStepTable[channel.StepIndex] >> 3;
            if ((nibble & 0x04) != 0) diff += IMAStepTable[channel.StepIndex];
            if ((nibble & 0x02) != 0) diff += IMAStepTable[channel.StepIndex] >> 1;
            if ((nibble & 0x01) != 0) diff += IMAStepTable[channel.StepIndex] >> 2;
            if ((nibble & 0x08) != 0)
                channel.Predictor -= diff;
            else
                channel.Predictor += diff;


            // Clamp result to 16-bit, -32768 - 32767
            channel.Predictor = Clamp(channel.Predictor, short.MinValue, short.MaxValue);

            // Find new index value (for later)
            channel.StepIndex += IMAIndexTable[nibble];
            channel.StepIndex = Clamp(channel.StepIndex, 0, 88);

            return (short)channel.Predictor;
        }

        private static void DecodeAdpcmImaWav(Decoder decoder, BinaryReader reader, BinaryWriter writer)
        {
            // reference implementations:
            // https://wiki.multimedia.cx/index.php/IMA_ADPCM
            // https://github.com/Nanook/TheGHOST/blob/master/ImaAdpcmPlugin/Ima.cs
            // https://github.com/rochars/imaadpcm/blob/master/index.js

            DecoderState state = decoder.State;
            AdpcmImaWavChannel[] channel = new AdpcmImaWavChannel[2];
            int nibbles = 0;
            bool isStereo = decoder.AudioFormat.Channels == 2 ? true : false;

            // https://www.microchip.com/forums/m698891.aspx
            // Each block starts with a header consisting of the following 4 bytes:
            //  16 bit audio sample (2 bytes, little endian)
            //   8 bit step table index
            //   dummy byte (set to zero)
            channel[0].Predictor = reader.ReadInt16();
            channel[0].StepIndex = reader.ReadByte();
            channel[0].StepIndex = Clamp(channel[0].StepIndex, 0, 88);
            reader.ReadByte();

            if (isStereo)
            {
                channel[1].Predictor = reader.ReadInt16();
                channel[1].StepIndex = reader.ReadByte();
                channel[1].StepIndex = Clamp(channel[1].StepIndex, 0, 88);
                reader.ReadByte();
            }

            // Note that we encode two samples per byte, 
            // but there are an odd number samples per block.
            // One of the samples is in the ADPCM block header. 
            // So, a block looks like this:

            // Example: BlockAlign 2048, SamplesPerBlock 4089
            // 4 bytes, Block header including 1 sample
            // 2048-4 = 2044 bytes with 4089-1 = 4088 samples
            // Total of 4089 samples per block.

            // Example: BlockAlign 512, SamplesPerBlock 505
            // 4 bytes, Block header including 1 sample
            // 512-4 = 508 bytes with 505-1 = 504 samples
            // Total of 505 samples per block.

            if (isStereo)
            {
                int offset = 0;
                short[] sample = new short[2 * (state.BlockAlign - 8)];
                for (nibbles = 2 * (state.BlockAlign - 8);
                    nibbles > 0;
                    nibbles -= 16)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        byte buffer = reader.ReadByte();
                        sample[offset + i * 4 + 0] = AdpcmImaWavExpandNibble(ref channel[0], buffer & 0x0f);
                        sample[offset + i * 4 + 2] = AdpcmImaWavExpandNibble(ref channel[0], buffer >> 4);
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        byte buffer = reader.ReadByte();
                        sample[offset + i * 4 + 1] = AdpcmImaWavExpandNibble(ref channel[1], buffer & 0x0f);
                        sample[offset + i * 4 + 3] = AdpcmImaWavExpandNibble(ref channel[1], buffer >> 4);
                    }

                    offset += 16;
                }

                for (int i = 0; i < sample.Length; i++)
                {
                    writer.Write(sample[i]);
                }
            }
            else
            {
                for (nibbles = 2 * (state.BlockAlign - 4);
                    nibbles > 0;
                    nibbles -= 2)
                {
                    byte buffer = reader.ReadByte();
                    writer.Write(AdpcmImaWavExpandNibble(ref channel[0], (buffer) & 0x0f));
                    writer.Write(AdpcmImaWavExpandNibble(ref channel[0], (buffer) >> 4));
                }
            }
        }

        /*
         * Ima4 in QT file
         */
        private static void DecodeAdpcmImaQT(Decoder decoder, BinaryReader reader, BinaryWriter writer)
        {
            // https://wiki.multimedia.cx/index.php/Apple_QuickTime_IMA_ADPCM
            // In any given IMA-encoded QuickTime file, 
            // the size of an individual block of IMA nibbles is stored in the bytes/packet field present 
            // in the extended audio information portion in an audio stsd atom. 
            // However, this size always seems to be 34 bytes/block. 
            // Sometimes, IMA-encoded Quicktime files are missing the extended wave information header. 
            // In this case, assume that each IMA block is 34 bytes.

            AdpcmImaWavChannel[] channel = new AdpcmImaWavChannel[2];

            int channels = decoder.AudioFormat.Channels;

            for (int i = 0; i < channels; i++)
            {
                // The first 2 bytes of a block specify a preamble with the initial predictor and step index. 
                // The 2 bytes are read from the stream as a big-endian 16-bit number which has the following bit structure:
                // pppppppp piiiiiii 
                // Bits 15-7 of the preamble are the top 9 bits of the initial signed predictor; 
                // Bits 6-0 of the initial predictor are always 0. 
                // Bits 6-0 of the preamble specify the initial step index. 
                // Note that this gives a range of 0..127 which should be clamped to 0..88 for good measure.
                byte buffer0 = reader.ReadByte();
                byte buffer1 = reader.ReadByte();
                channel[i].Predictor = (short)((((buffer0 << 1) | (buffer1 >> 7))) << 7);
                channel[i].StepIndex = buffer1 & 0x7f;
                channel[i].StepIndex = Clamp(channel[i].StepIndex, 0, 88);

                // The remaining bytes in the IMA block (of which there are usually 32) are the ADPCM nibbles. 
                // In Quicktime IMA data, the bottom nibble of a byte is decoded first, then the top nibble:
                for (int nibbles = 0; nibbles < 64; nibbles += 2)
                {
                    byte buffer = reader.ReadByte();
                    writer.Write(AdpcmImaWavExpandNibble(ref channel[i], (buffer) & 0x0f));
                    writer.Write(AdpcmImaWavExpandNibble(ref channel[i], (buffer >> 4) & 0x0f));
                }
            }
        }

        /*
         * Dk4
         */
        private static void DecodeAdpcmDk4(Decoder decoder, BinaryReader reader, BinaryWriter writer)
        {
            // https://wiki.multimedia.cx/index.php/Duck_DK4_IMA_ADPCM

            DecoderState state = decoder.State;
            AdpcmImaWavChannel[] channel = new AdpcmImaWavChannel[2];

            bool isStereo = decoder.AudioFormat.Channels == 2 ? true : false;

            // If the DK4 data is stereo, a chunk begins with two preambles, one for the left audio channel and one for the right audio channel:
            // bytes 0-1:  initial predictor (in little-endian format) for left channel
            // byte 2:     initial index for left channel
            // byte 3:     unknown, usually 0 and is probably reserved
            // bytes 4-5:  initial predictor (in little-endian format) for right channel
            // byte 6:     initial index (for right channel)
            // byte 7:     unknown, usually 0 and is probably reserved 

            channel[0].Predictor = reader.ReadInt16();
            channel[0].StepIndex = reader.ReadByte();
            channel[0].StepIndex = Clamp(channel[0].StepIndex, 0, 88);
            reader.ReadByte();

            if (isStereo)
            {
                channel[1].Predictor = reader.ReadInt16();
                channel[1].StepIndex = reader.ReadByte();
                channel[1].StepIndex = Clamp(channel[1].StepIndex, 0, 88);
                reader.ReadByte();
            }

            /* first output predictor */
            writer.Write(channel[0].Predictor);
            if (isStereo)
            {
                writer.Write(channel[1].Predictor);
            }

            for (int nibbles = 0;
                 nibbles < state.BlockAlign - 4 * (isStereo ? 2 : 1);
                 nibbles++)
            {
                byte buffer = reader.ReadByte();
                writer.Write(AdpcmImaWavExpandNibble(ref channel[0], (buffer) >> 4));
                writer.Write(AdpcmImaWavExpandNibble(ref channel[isStereo ? 1 : 0], (buffer) & 0x0f));

                reader.ReadByte();
            }
        }

        /*
         * Dk3
         */
        private static void DecodeAdpcmDk3(Decoder decoder, BinaryReader reader, BinaryWriter writer)
        {
            // https://wiki.multimedia.cx/index.php/Duck_DK3_IMA_ADPCM

            DecoderState state = decoder.State;
            AdpcmImaWavChannel sum;
            AdpcmImaWavChannel diff;

            // A block of DK3 has a 16-byte preamble with the following information:
            // bytes 0-1     unknown
            // bytes 2-3     sample rate
            // bytes 4-9     unknown
            // bytes 10-11   initial sum channel predictor
            // bytes 12-13   initial diff channel predictor
            // byte 14       initial sum channel index
            // byte 15       initial diff channel index 

            reader.ReadBytes(10); // skip

            sum.Predictor = reader.ReadInt16();
            diff.Predictor = reader.ReadInt16();
            sum.StepIndex = reader.ReadByte();
            diff.StepIndex = reader.ReadByte();

            int diffValue = diff.Predictor;

            // Each set of 3 nibbles decodes to 4 16-bit PCM samples using this process 
            // (note that the diff value is initialized to the same value as the diff predictor)
            /* we process 6 nibbles at once */
            byte buff = 0;
            for (int i = 16; i < state.BlockAlign; i++)
            {
                // get next ADPCM nibble in stream
                buff = reader.ReadByte();

                /* first 3 nibbles */
                AdpcmImaWavExpandNibble(ref sum, (buff) & 0x0f);
                AdpcmImaWavExpandNibble(ref diff, (buff) >> 4);

                diffValue = (diffValue + diff.Predictor) / 2;

                writer.Write(sum.Predictor + diffValue);
                writer.Write(sum.Predictor - diffValue);

                buff = reader.ReadByte();

                AdpcmImaWavExpandNibble(ref sum, (buff) & 0x0f);

                writer.Write(sum.Predictor + diffValue);
                writer.Write(sum.Predictor - diffValue);

                /* now last 3 nibbles */
                AdpcmImaWavExpandNibble(ref sum, (buff) >> 4);

                buff = reader.ReadByte();
                if (i < state.BlockAlign)
                {
                    AdpcmImaWavExpandNibble(ref diff, (buff) & 0x0f);

                    diffValue = (diffValue + diff.Predictor) / 2;

                    writer.Write(sum.Predictor + diffValue);
                    writer.Write(sum.Predictor - diffValue);

                    AdpcmImaWavExpandNibble(ref sum, (buff) >> 4);

                    buff = reader.ReadByte();

                    writer.Write(sum.Predictor + diffValue);
                    writer.Write(sum.Predictor - diffValue);
                }
            }
        }


        public struct AdpcmEASpl
        {
            public UInt32 u; // unsigned
            public Int32 i; // signed
        }

        /*
         * EA ADPCM
         */
        private static void DecodeAdpcmEA(Decoder decoder, BinaryReader reader, BinaryWriter writer)
        {
            int[] EATable =
            {
                0x0000, 0x00F0, 0x01CC, 0x0188, 0x0000, 0x0000, 0xFF30, 0xFF24,
                0x0000, 0x0001, 0x0003, 0x0004, 0x0007, 0x0008, 0x000A, 0x000B,
                0x0000, 0xFFFF, 0xFFFD, 0xFFFC
            };

            DecoderState state = decoder.State;
            int[] c1 = new int[2];
            int[] c2 = new int[2];
            int[] d = new int[2];

            int channels = decoder.AudioFormat.Channels;
            short[] prev = state.Prev;
            int[] cur = new int[prev.Length + channels];

            for (int c = 0; c < channels; c++)
            {
                byte input = reader.ReadByte();

                c1[c] = EATable[input >> 4];
                c2[c] = EATable[(input >> 4) + 4];
                d[c] = (input & 0xf) + 8;
            }

            for (int i = 0; i < state.BlockAlign; i += channels)
            {
                AdpcmEASpl spl = new AdpcmEASpl();

                for (int c = 0; c < channels; c++)
                {
                    byte buffer = reader.ReadByte();
                    spl.u = (uint)((buffer & 0xf0u) << 24);
                    spl.i >>= d[c];
                    spl.i = (spl.i + cur[c] * c1[c] + prev[c] * c2[c] + 0x80) >> 8;

                    // Clamp result to 16-bit, -32768 - 32767
                    spl.i = Clamp(spl.i, short.MinValue, short.MaxValue);

                    prev[c] = (short)cur[c];
                    cur[c] = spl.i;

                    writer.Write((short)spl.i);
                }

                for (int c = 0; c < channels; c++)
                {
                    byte buffer = reader.ReadByte();
                    spl.u = (uint)(buffer & 0x0fu) << 28;
                    spl.i >>= d[c];
                    spl.i = (spl.i + cur[c] * c1[c] + prev[c] * c2[c] + 0x80) >> 8;

                    // Clamp result to 16-bit, -32768 - 32767
                    spl.i = Clamp(spl.i, short.MinValue, short.MaxValue);

                    prev[c] = (short)cur[c];
                    cur[c] = spl.i;

                    writer.Write((short)spl.i);
                }
            }
        }

        // util to clamp a number within a given range
        private static int Clamp(int num, int min, int max)
        {
            return num <= min ? min : num >= max ? max : num;
        }
    }
}