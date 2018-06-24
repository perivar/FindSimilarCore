namespace FindSimilarServices.CSCore.Codecs.ADPCM
{
    // see https://github.com/jwzhangjie/Adpcm_Pcm/blob/master/Adpcm.c
    // https://sourceforge.net/p/openbor/tools/3174/tree/tools/openwav2bor/source/adpcm.c?diff=50c8e1ce1be1ce03cfa5c218:3173
    // https://gist.github.com/jaames/c837fb87a6a5585d47baa1b8e2408234
    // https://github.com/srnsw/xena/blob/master/plugins/audio/ext/src/tritonus/src/classes/org/tritonus/sampled/convert/ImaAdpcmFormatConversionProvider.java
    // https://github.com/lguipeng/TwoWayRadio/blob/master/app/src/main/java/com/szu/twowayradio/utils/Adpcm.java

    public class AdpcmState
    {
        /* Previous output value */
        public int[] PreviousSample = new int[2];

        /* Index into stepsize table */
        public int[] PreviousIndex = new int[2];
    }

    public class Adpcm
    {
        AdpcmState state = null;

        private bool isBigEndian = false;

        // variables for the decode IMA implementation
        private int predictedSample = 0;
        private int index = 0;
        private int stepSize = 7;

        static int[] stepIndexTable =
        {
            -1, -1, -1, -1, 2, 4, 6, 8,
            -1, -1, -1, -1, 2, 4, 6, 8,
            };

        static int[] stepSizeTable =
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

        /**
        * Creates a ADPCM encoder/decoder.
        */
        public Adpcm(bool isBigEndian = false)
        {
            this.isBigEndian = isBigEndian;
            this.state = new AdpcmState();
        }

        // len = input buffer size in bytes
        // see https://github.com/rofl0r/openbor-legacy/blob/master/source/adpcmlib/adpcm.c
        public int AdpcmDecode(byte[] inBuffer, byte[] outBuffer, int len, int channels)
        {
            if (channels == 2)
            {
                return AdpcmDecodeStereo(inBuffer, outBuffer, len);
            }
            return AdpcmDecodeStereo(inBuffer, outBuffer, len);
        }

        // len = input buffer size in bytes
        // see https://github.com/rofl0r/openbor-legacy/blob/master/source/adpcmlib/adpcm.c
        public int AdpcmDecode(byte[] inBuffer, float[] outBuffer, int len, int channels)
        {
            if (channels == 2)
            {
                return AdpcmDecodeStereo(inBuffer, outBuffer, len);
            }
            return AdpcmDecodeStereo(inBuffer, outBuffer, len);
        }


        // len = input buffer size in bytes
        // see https://github.com/rofl0r/openbor-legacy/blob/master/source/adpcmlib/adpcm.c
        public int AdpcmDecodeMono(byte[] inBuffer, byte[] outBuffer, int len)
        {
            int sign;            /* Current Adpcm sign bit */
            int delta;           /* Current Adpcm output value */
            int step;            /* Stepsize */
            int valuePred;       /* Predicted value */
            int valuePredDiff;   /* Current change to valpred */
            int stepIndex;       /* Current step change index */
            int inputBuffer = 0; /* place to keep next 4-bit value */
            int bytesDecoded = 0;

            if (inBuffer.Length == 0 || outBuffer.Length == 0 || len < 1)
                return 0;

            len *= 2;
            valuePred = state.PreviousSample[0];
            stepIndex = state.PreviousIndex[0];
            step = stepSizeTable[stepIndex];

            int inputIndex = 0;
            int outputIndex = 0;
            for (bytesDecoded = 0; bytesDecoded < len; bytesDecoded++)
            {
                /* Step 1 - Get the delta value */
                if ((bytesDecoded & 1) != 0)
                {
                    delta = inputBuffer & 0xf;
                }
                else
                {
                    inputBuffer = inBuffer[inputIndex++];
                    delta = (inputBuffer >> 4) & 0xf;
                }

                /* Step 2 - Find new index value (for later) */
                stepIndex += stepIndexTable[delta];
                if (stepIndex < 0) stepIndex = 0;
                if (stepIndex > 88) stepIndex = 88;

                /* Step 3 - Separate sign and magnitude */
                sign = delta & 8;
                delta = delta & 7;

                /* Step 4 - Compute difference and new predicted value */
                /*
                ** Computes 'vpdiff = (delta+0.5)*step/4', but see comment
                ** in Adpcm_coder.
                */
                valuePredDiff = step >> 3;
                if ((delta & 4) != 0) valuePredDiff += step;
                if ((delta & 2) != 0) valuePredDiff += step >> 1;
                if ((delta & 1) != 0) valuePredDiff += step >> 2;

                // handle sign bit
                if (sign != 0)
                    valuePred -= valuePredDiff;
                else
                    valuePred += valuePredDiff;

                /* Step 5 - clamp output value */
                if (valuePred > 32767)
                    valuePred = 32767;
                else if (valuePred < -32768)
                    valuePred = -32768;

                /* Step 6 - Update step value */
                step = stepSizeTable[stepIndex];

                /* Step 7 - Output value */
                if (isBigEndian)
                {
                    outBuffer[outputIndex++] = (byte)(valuePred >> 8);
                    outBuffer[outputIndex++] = (byte)(valuePred & 0xFF);
                }
                else
                {
                    outBuffer[outputIndex++] = (byte)(valuePred & 0xFF);
                    outBuffer[outputIndex++] = (byte)(valuePred >> 8);
                }
            }

            state.PreviousSample[0] = valuePred;
            state.PreviousIndex[0] = stepIndex;
            return bytesDecoded * 2;
        }


        // len = input buffer size in bytes
        // see https://github.com/rofl0r/openbor-legacy/blob/master/source/adpcmlib/adpcm.c
        public int AdpcmDecodeStereo(byte[] inBuffer, byte[] outBuffer, int len)
        {
            int sign = 0;               /* Current adpcm sign bit */
            int delta = 0;              /* Current adpcm output value */
            int[] step = { 0, 0 };      /* Stepsize */
            int[] valuePred = { 0, 0 }; /* Predicted value */
            int valuePredDiff = 0;      /* Current change to valpred */
            int[] index = { 0, 0 };     /* Current step change index */
            int inputBuffer = 0;        /* place to keep next 4-bit value */
            int bytesDecoded = 0;

            if (inBuffer.Length == 0 || outBuffer.Length == 0 || len < 1)
                return 0;

            valuePred[0] = state.PreviousSample[0];
            valuePred[1] = state.PreviousSample[1];
            index[0] = state.PreviousIndex[0];
            index[1] = state.PreviousIndex[1];
            step[0] = stepSizeTable[index[0]];
            step[1] = stepSizeTable[index[1]];

            int inputIndex = 0;
            int outputIndex = 0;
            for (bytesDecoded = 0; bytesDecoded < len; bytesDecoded++)
            {
                inputBuffer = inBuffer[inputIndex++];

                /* Left Channel */
                /* Step 1 - get the delta value */
                delta = (inputBuffer >> 4) & 0xf;

                /* Step 2 - Find new index value (for later) */
                index[0] += stepIndexTable[delta];
                if (index[0] < 0)
                    index[0] = 0;
                if (index[0] > 88)
                    index[0] = 88;

                /* Step 3 - Separate sign and magnitude */
                sign = delta & 8;
                delta = delta & 7;

                /* Step 4 - Compute difference and new predicted value */
                /*
                 ** Computes 'vpdiff = (delta+0.5)*step/4', but see comment
                 ** in adpcm_coder.
                 */
                valuePredDiff = step[0] >> 3;
                if ((delta & 4) != 0) valuePredDiff += step[0];
                if ((delta & 2) != 0) valuePredDiff += step[0] >> 1;
                if ((delta & 1) != 0) valuePredDiff += step[0] >> 2;

                if (sign != 0)
                    valuePred[0] -= valuePredDiff;
                else
                    valuePred[0] += valuePredDiff;

                /* Step 5 - clamp output value */
                if (valuePred[0] > 32767)
                    valuePred[0] = 32767;
                else if (valuePred[0] < -32768)
                    valuePred[0] = -32768;

                /* Step 6 - Update step value */
                step[0] = stepSizeTable[index[0]];

                /* Step 7 - Output value */
                if (isBigEndian)
                {
                    outBuffer[outputIndex++] = (byte)(valuePred[0] >> 8);
                    outBuffer[outputIndex++] = (byte)(valuePred[0] & 0xFF);
                }
                else
                {
                    outBuffer[outputIndex++] = (byte)(valuePred[0] & 0xFF);
                    outBuffer[outputIndex++] = (byte)(valuePred[0] >> 8);
                }

                /* Right Channel */
                /* Step 1 - get the delta value */
                delta = inputBuffer & 0xf;

                /* Step 2 - Find new index value (for later) */
                index[1] += stepIndexTable[delta];
                if (index[1] < 0)
                    index[1] = 0;
                if (index[1] > 88)
                    index[1] = 88;

                /* Step 3 - Separate sign and magnitude */
                sign = delta & 8;
                delta = delta & 7;

                /* Step 4 - Compute difference and new predicted value */
                /*
                 ** Computes 'vpdiff = (delta+0.5)*step/4', but see comment
                 ** in adpcm_coder.
                 */
                valuePredDiff = step[1] >> 3;
                if ((delta & 4) != 0) valuePredDiff += step[1];
                if ((delta & 2) != 0) valuePredDiff += step[1] >> 1;
                if ((delta & 1) != 0) valuePredDiff += step[1] >> 2;

                if (sign != 0)
                    valuePred[1] -= valuePredDiff;
                else
                    valuePred[1] += valuePredDiff;

                /* Step 5 - clamp output value */
                if (valuePred[1] > 32767)
                    valuePred[1] = 32767;
                else if (valuePred[1] < -32768)
                    valuePred[1] = -32768;

                /* Step 6 - Update step value */
                step[1] = stepSizeTable[index[1]];

                /* Step 7 - Output value */
                if (isBigEndian)
                {
                    outBuffer[outputIndex++] = (byte)(valuePred[1] >> 8);
                    outBuffer[outputIndex++] = (byte)(valuePred[1] & 0xFF);
                }
                else
                {
                    outBuffer[outputIndex++] = (byte)(valuePred[1] & 0xFF);
                    outBuffer[outputIndex++] = (byte)(valuePred[1] >> 8);
                }
            }

            state.PreviousSample[0] = valuePred[0];
            state.PreviousSample[1] = valuePred[1];
            state.PreviousIndex[0] = index[0];
            state.PreviousIndex[1] = index[1];

            return bytesDecoded * 4;
        }

        // len = input buffer size in bytes
        // see https://github.com/rofl0r/openbor-legacy/blob/master/source/adpcmlib/adpcm.c
        public int AdpcmDecodeMono(byte[] inBuffer, float[] outBuffer, int len)
        {
            int sign;            /* Current Adpcm sign bit */
            int delta;           /* Current Adpcm output value */
            int step;            /* Stepsize */
            int valuePred;       /* Predicted value */
            int valuePredDiff;   /* Current change to valpred */
            int stepIndex;       /* Current step change index */
            int inputBuffer = 0; /* place to keep next 4-bit value */
            int bytesDecoded = 0;

            if (inBuffer.Length == 0 || outBuffer.Length == 0 || len < 1)
                return 0;

            len *= 2;
            valuePred = state.PreviousSample[0];
            stepIndex = state.PreviousIndex[0];
            step = stepSizeTable[stepIndex];

            int inputIndex = 0;
            int outputIndex = 0;
            for (bytesDecoded = 0; bytesDecoded < len; bytesDecoded++)
            {
                /* Step 1 - Get the delta value */
                if ((bytesDecoded & 1) != 0)
                {
                    delta = inputBuffer & 0xf;
                }
                else
                {
                    inputBuffer = inBuffer[inputIndex++];
                    delta = (inputBuffer >> 4) & 0xf;
                }

                /* Step 2 - Find new index value (for later) */
                stepIndex += stepIndexTable[delta];
                if (stepIndex < 0) stepIndex = 0;
                if (stepIndex > 88) stepIndex = 88;

                /* Step 3 - Separate sign and magnitude */
                sign = delta & 8;
                delta = delta & 7;

                /* Step 4 - Compute difference and new predicted value */
                /*
                ** Computes 'vpdiff = (delta+0.5)*step/4', but see comment
                ** in Adpcm_coder.
                */
                valuePredDiff = step >> 3;
                if ((delta & 4) != 0) valuePredDiff += step;
                if ((delta & 2) != 0) valuePredDiff += step >> 1;
                if ((delta & 1) != 0) valuePredDiff += step >> 2;

                // handle sign bit
                if (sign != 0)
                    valuePred -= valuePredDiff;
                else
                    valuePred += valuePredDiff;

                /* Step 5 - clamp output value */
                if (valuePred > 32767)
                    valuePred = 32767;
                else if (valuePred < -32768)
                    valuePred = -32768;

                /* Step 6 - Update step value */
                step = stepSizeTable[stepIndex];

                /* Step 7 - Output value */
                outBuffer[outputIndex++] = (float)valuePred / 32768;
            }

            state.PreviousSample[0] = valuePred;
            state.PreviousIndex[0] = stepIndex;
            return bytesDecoded * 2;
        }


        // len = input buffer size in bytes
        // see https://github.com/rofl0r/openbor-legacy/blob/master/source/adpcmlib/adpcm.c
        public int AdpcmDecodeStereo(byte[] inBuffer, float[] outBuffer, int len)
        {
            int sign = 0;               /* Current adpcm sign bit */
            int delta = 0;              /* Current adpcm output value */
            int[] step = { 0, 0 };      /* Stepsize */
            int[] valuePred = { 0, 0 }; /* Predicted value */
            int valuePredDiff = 0;      /* Current change to valpred */
            int[] index = { 0, 0 };     /* Current step change index */
            int inputBuffer = 0;        /* place to keep next 4-bit value */
            int bytesDecoded = 0;

            if (inBuffer.Length == 0 || outBuffer.Length == 0 || len < 1)
                return 0;

            valuePred[0] = state.PreviousSample[0];
            valuePred[1] = state.PreviousSample[1];
            index[0] = state.PreviousIndex[0];
            index[1] = state.PreviousIndex[1];
            step[0] = stepSizeTable[index[0]];
            step[1] = stepSizeTable[index[1]];

            int inputIndex = 0;
            int outputIndex = 0;
            for (bytesDecoded = 0; bytesDecoded < len; bytesDecoded++)
            {
                inputBuffer = inBuffer[inputIndex++];

                /* Left Channel */
                /* Step 1 - get the delta value */
                delta = (inputBuffer >> 4) & 0xf;

                /* Step 2 - Find new index value (for later) */
                index[0] += stepIndexTable[delta];
                if (index[0] < 0)
                    index[0] = 0;
                if (index[0] > 88)
                    index[0] = 88;

                /* Step 3 - Separate sign and magnitude */
                sign = delta & 8;
                delta = delta & 7;

                /* Step 4 - Compute difference and new predicted value */
                /*
                 ** Computes 'vpdiff = (delta+0.5)*step/4', but see comment
                 ** in adpcm_coder.
                 */
                valuePredDiff = step[0] >> 3;
                if ((delta & 4) != 0) valuePredDiff += step[0];
                if ((delta & 2) != 0) valuePredDiff += step[0] >> 1;
                if ((delta & 1) != 0) valuePredDiff += step[0] >> 2;

                if (sign != 0)
                    valuePred[0] -= valuePredDiff;
                else
                    valuePred[0] += valuePredDiff;

                /* Step 5 - clamp output value */
                if (valuePred[0] > 32767)
                    valuePred[0] = 32767;
                else if (valuePred[0] < -32768)
                    valuePred[0] = -32768;

                /* Step 6 - Update step value */
                step[0] = stepSizeTable[index[0]];

                /* Step 7 - Output value */
                outBuffer[outputIndex++] = (float)valuePred[0] / 32768;

                /* Right Channel */
                /* Step 1 - get the delta value */
                delta = inputBuffer & 0xf;

                /* Step 2 - Find new index value (for later) */
                index[1] += stepIndexTable[delta];
                if (index[1] < 0)
                    index[1] = 0;
                if (index[1] > 88)
                    index[1] = 88;

                /* Step 3 - Separate sign and magnitude */
                sign = delta & 8;
                delta = delta & 7;

                /* Step 4 - Compute difference and new predicted value */
                /*
                 ** Computes 'vpdiff = (delta+0.5)*step/4', but see comment
                 ** in adpcm_coder.
                 */
                valuePredDiff = step[1] >> 3;
                if ((delta & 4) != 0) valuePredDiff += step[1];
                if ((delta & 2) != 0) valuePredDiff += step[1] >> 1;
                if ((delta & 1) != 0) valuePredDiff += step[1] >> 2;

                if (sign != 0)
                    valuePred[1] -= valuePredDiff;
                else
                    valuePred[1] += valuePredDiff;

                /* Step 5 - clamp output value */
                if (valuePred[1] > 32767)
                    valuePred[1] = 32767;
                else if (valuePred[1] < -32768)
                    valuePred[1] = -32768;

                /* Step 6 - Update step value */
                step[1] = stepSizeTable[index[1]];

                /* Step 7 - Output value */
                outBuffer[outputIndex++] = (float)valuePred[1] / 32768;
            }

            state.PreviousSample[0] = valuePred[0];
            state.PreviousSample[1] = valuePred[1];
            state.PreviousIndex[0] = index[0];
            state.PreviousIndex[1] = index[1];

            return bytesDecoded * 4;
        }

        /// <summary>
        /// Decode 4-bit ADPCM samples into 16-bit linear PCM data (signed).     
        /// </summary>
        /// <param name="raw">raw 4-bit ADPCM samples</param>
        /// <param name="offset">offset</param>
        /// <param name="len">length</param>
        /// <returns>Decoded samples</returns>
        public byte[] DecodeIma(byte[] raw, int offset, int len)
        {
            // see https://github.com/UFAL-DSG/alex-android/blob/master/app/src/main/java/cz/cuni/mff/ufal/androidalex/ADPCMDecoder.java
            byte[] ret = new byte[len * 4];

            // Use 2x length because we examine each byte twice.
            for (int i = offset; i < (offset + len) * 2; i++)
            {
                int originalSample; // 4 bits
                if (i % 2 != 0)   // odd i
                {
                    originalSample = (raw[i / 2] & 0xFF) & 0x0F;   //bottom four bits
                }
                else                // even i
                {
                    originalSample = (raw[i / 2] & 0xFF) >> 4;     //top four bits
                }

                // compute difference and new predicted value
                int difference = 0;
                if ((originalSample & 4) != 0)  //b0000 0100
                {
                    difference += stepSize;
                }
                if ((originalSample & 2) != 0)  //b0000 0010
                {
                    difference += stepSize >> 1;
                }
                if ((originalSample & 1) != 0)  //b0000 0001
                {
                    difference += stepSize >> 2;
                }

                difference += stepSize >> 3;

                // handle sign bit
                if ((originalSample & 8) != 0)  //b0000 1000
                {
                    difference = -difference;
                }

                predictedSample += difference;

                // clamp output value
                if (predictedSample > 32767)
                    predictedSample = 32767;
                else if (predictedSample < -32768)
                    predictedSample = -32768;

                // Output value
                if (isBigEndian)
                {
                    ret[i * 2] = (byte)(predictedSample >> 8);        // top 8 bits
                    ret[i * 2 + 1] = (byte)(predictedSample & 0xFF);  // bottom 8 bits                
                }
                else
                {
                    ret[i * 2] = (byte)(predictedSample & 0xFF);    // top 8 bits
                    ret[i * 2 + 1] = (byte)(predictedSample >> 8);  // bottom 8 bits
                }

                // find new index value
                index += stepIndexTable[originalSample];
                if (index < 0)
                    index = 0;
                else if (index > 88)  // Size of step_table
                    index = 88;

                stepSize = stepSizeTable[index];
            }
            return ret;
        }

        /// <summary>
        /// Decode 4-bit ADPCM samples into 32-bit ieee PCM data
        /// </summary>
        /// <param name="raw">raw 4-bit ADPCM samples</param>
        /// <param name="offset">offset</param>
        /// <param name="len">length</param>
        /// <returns>Decoded samples</returns>
        public float[] DecodeImaFloats(byte[] raw, int offset, int len)
        {
            // see https://github.com/UFAL-DSG/alex-android/blob/master/app/src/main/java/cz/cuni/mff/ufal/androidalex/ADPCMDecoder.java
            // https://github.com/rochars/imaadpcm/blob/master/index.js
            float[] ret = new float[len * 2];

            // Use 2x length because we examine each byte twice.
            for (int i = offset; i < (offset + len) * 2; i++)
            {
                int originalSample; // 4 bits
                if (i % 2 != 0)   // odd i
                {
                    originalSample = (raw[i / 2] & 0xFF) & 0x0F;   //bottom four bits
                }
                else                // even i
                {
                    originalSample = (raw[i / 2] & 0xFF) >> 4;     //top four bits
                }

                // compute difference and new predicted value
                int difference = 0;
                if ((originalSample & 4) != 0)  //b0000 0100
                {
                    difference += stepSize;
                }
                if ((originalSample & 2) != 0)  //b0000 0010
                {
                    difference += stepSize >> 1;
                }
                if ((originalSample & 1) != 0)  //b0000 0001
                {
                    difference += stepSize >> 2;
                }

                difference += stepSize >> 3;

                // handle sign bit
                if ((originalSample & 8) != 0)  //b0000 1000
                {
                    difference = -difference;
                }

                predictedSample += difference;

                // clamp output value
                if (predictedSample > 32767)
                    predictedSample = 32767;
                else if (predictedSample < -32768)
                    predictedSample = -32768;

                // Output value
                ret[i] = (float)predictedSample / 32768;

                // find new index value
                index += stepIndexTable[originalSample];
                if (index < 0)
                    index = 0;
                else if (index > 88)  // Size of step_table
                    index = 88;

                stepSize = stepSizeTable[index];
            }
            return ret;
        }

        // see https://gist.github.com/jaames/c837fb87a6a5585d47baa1b8e2408234
        public float[] DecodeAdpcmMono(byte[] inputBuffer)
        {
            state.PreviousSample[0] = 0;
            state.PreviousIndex[0] = 0;

            var outputBuffer = new float[inputBuffer.Length * 2];
            var outputBufferOffset = 0;
            for (int inputBufferOffset = 0; inputBufferOffset < inputBuffer.Length; inputBufferOffset++)
            {
                var inputByte = inputBuffer[inputBufferOffset];
                outputBuffer[outputBufferOffset] = DecodeSample((inputByte >> 4) & 0xF);
                outputBuffer[outputBufferOffset + 1] = DecodeSample(inputByte & 0xF);
                outputBufferOffset += 2;
            }
            return outputBuffer;
        }

        private float DecodeSample(int sample)
        {
            var predSample = state.PreviousSample[0];
            var index = state.PreviousIndex[0];
            var step = stepSizeTable[index];
            var difference = step >> 3;

            // compute difference and new predicted value
            if ((sample & 0x4) != 0) difference += step;
            if ((sample & 0x2) != 0) difference += (step >> 1);
            if ((sample & 0x1) != 0) difference += (step >> 2);

            // handle sign bit
            predSample += ((sample & 0x8) != 0) ? -difference : difference;

            // find new index value
            index += stepIndexTable[sample];
            index = Clamp(index, 0, 88);

            // clamp output value
            predSample = Clamp(predSample, -32767, 32767);
            state.PreviousSample[0] = predSample;
            state.PreviousIndex[0] = index;

            // return a value between -1.0 and 1.0, since that's what's used by JavaScript's AudioBuffer API
            return (float)predSample / 32768;
        }

        // util to clamp a number within a given range
        private int Clamp(int num, int min, int max)
        {
            return num <= min ? min : num >= max ? max : num;
        }
    }
}
