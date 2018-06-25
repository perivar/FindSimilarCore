using System;
using System.IO;
using System.Text;
using CommonUtils;

namespace FindSimilarServices.CSCore.Codecs.ADPCM
{
    public class AdpcmMS
    {
        /**
         * A bunch of magical numbers that predict the sample data from the
         * MSADPCM wavedata. Do not attempt to understand at all costs!
         */
        private static readonly int[] AdaptionTable = {
            230, 230, 230, 230, 307, 409, 512, 614,
            768, 614, 512, 409, 307, 230, 230, 230
        };
        private static readonly int[] AdaptCoeff_1 = {
            256, 512, 0, 192, 240, 460, 392
        };
        private static readonly int[] AdaptCoeff_2 = {
            0, -256, 0, 64, 0, -208, -232
        };

        /**
		 * Splits the MSADPCM samples from each byte block.
		 * @param block An MSADPCM sample byte
		 * @param nibbleBlock we copy the parsed shorts into here
		 */
        private static void GetNibbleBlock(byte block, byte[] nibbleBlock)
        {
            nibbleBlock[0] = (byte)(block >> 4); // Upper half
            nibbleBlock[1] = (byte)(block & 0xF); // Lower half
        }

        /**
		 * Calculates PCM samples based on previous samples and a nibble input.
		 * @param nibble A parsed MSADPCM sample we got from getNibbleBlock
		 * @param predictor The predictor we get from the MSADPCM block's preamble
		 * @param sample_1 The first sample we use to predict the next sample
		 * @param sample_2 The second sample we use to predict the next sample
		 * @param delta Used to calculate the final sample
		 * @return The calculated PCM sample
		 */
        private static short CalculateSample(
            byte nibble,
            byte predictor,
            ref short sample_1,
            ref short sample_2,
            ref short delta
        )
        {
            if (predictor < 0 || predictor > 6) throw new ArgumentException("ADPCMMS failed, predictor is outside the range 0-6: " + predictor);

            // Get a signed number out of the nibble. We need to retain the
            // original nibble value for when we access AdaptionTable[].
            sbyte signedNibble = (sbyte)nibble;
            if ((signedNibble & 0x8) == 0x8)
            {
                signedNibble -= 0x10;
            }

            // Calculate new sample
            int sampleInt = (
                ((sample_1 * AdaptCoeff_1[predictor]) +
                    (sample_2 * AdaptCoeff_2[predictor])
                ) / 256
            );
            sampleInt += signedNibble * delta;

            // Clamp result to 16-bit
            short sample;
            if (sampleInt < short.MinValue)
            {
                sample = short.MinValue;
            }
            else if (sampleInt > short.MaxValue)
            {
                sample = short.MaxValue;
            }
            else
            {
                sample = (short)sampleInt;
            }

            // Shuffle samples, get new delta
            sample_2 = sample_1;
            sample_1 = sample;
            delta = (short)(AdaptionTable[nibble] * delta / 256);

            // Saturate the delta to a lower bound of 16
            if (delta < 16)
                delta = 16;

            return sample;
        }

        /**
		 * Decodes MSADPCM data to signed 16-bit PCM data.
		 * @param Source A BinaryReader containing the headerless MSADPCM data
		 * @param numChannels The number of channels (WAVEFORMATEX nChannels)
		 * @param blockAlign The ADPCM block size (WAVEFORMATEX nBlockAlign)
		 * @return A byte array containing the raw 16-bit PCM wavedata
		 */
        public static byte[] ConvertToPCM(
            BinaryReader source,
            int numChannels,
            int blockAlign
        )
        {
            // We write to output when reading the PCM data, then we convert
            // it back to a short array at the end.
            MemoryStream output = new MemoryStream();
            BinaryWriter pcmOut = new BinaryWriter(output);

            // We'll be using this to get each sample from the blocks.
            byte[] nibbleBlock = new byte[2];

            // Assuming the whole stream is what we want.
            long fileLength = source.BaseStream.Length - blockAlign;

            // Mono or Stereo?
            if (numChannels == 1)
            {
                // Read to the end of the file.
                while (source.BaseStream.Position <= fileLength)
                {
                    // Read block preamble
                    byte predictor = source.ReadByte();
                    short delta = source.ReadInt16();
                    short sample_1 = source.ReadInt16();
                    short sample_2 = source.ReadInt16();

                    // Send the initial samples straight to PCM out.
                    pcmOut.Write(sample_2);
                    pcmOut.Write(sample_1);

                    // Go through the bytes in this MSADPCM block.
                    for (int bytes = 0; bytes < (blockAlign + 15); bytes++)
                    {
                        // Each sample is one half of a nibbleBlock.
                        GetNibbleBlock(source.ReadByte(), nibbleBlock);
                        for (int i = 0; i < 2; i++)
                        {
                            pcmOut.Write(
                                CalculateSample(
                                    nibbleBlock[i],
                                    predictor,
                                    ref sample_1,
                                    ref sample_2,
                                    ref delta
                                )
                            );
                        }
                    }
                }
            }
            else if (numChannels == 2)
            {
                // Read to the end of the file.
                while (source.BaseStream.Position <= fileLength)
                {
                    // Read block preamble
                    byte l_predictor = source.ReadByte();
                    byte r_predictor = source.ReadByte();
                    short l_delta = source.ReadInt16();
                    short r_delta = source.ReadInt16();
                    short l_sample_1 = source.ReadInt16();
                    short r_sample_1 = source.ReadInt16();
                    short l_sample_2 = source.ReadInt16();
                    short r_sample_2 = source.ReadInt16();

                    // Send the initial samples straight to PCM out.
                    pcmOut.Write(l_sample_2);
                    pcmOut.Write(r_sample_2);
                    pcmOut.Write(l_sample_1);
                    pcmOut.Write(r_sample_1);

                    // Go through the bytes in this MSADPCM block.
                    for (int bytes = 0; bytes < ((blockAlign + 15) * 2); bytes++)
                    {
                        // Each block carries one left/right sample.
                        GetNibbleBlock(source.ReadByte(), nibbleBlock);

                        // Left channel...
                        pcmOut.Write(
                            CalculateSample(
                                nibbleBlock[0],
                                l_predictor,
                                ref l_sample_1,
                                ref l_sample_2,
                                ref l_delta
                            )
                        );

                        // Right channel...
                        pcmOut.Write(
                            CalculateSample(
                                nibbleBlock[1],
                                r_predictor,
                                ref r_sample_1,
                                ref r_sample_2,
                                ref r_delta
                            )
                        );
                    }
                }
            }
            else
            {
                System.Console.WriteLine("MSADPCM WAVEDATA IS NOT MONO OR STEREO!");
                pcmOut.Close();
                output.Close();
                return null;
            }

            // We're done writing PCM data...
            pcmOut.Close();
            output.Close();

            // Return the array.
            return output.ToArray();
        }
    }
}
