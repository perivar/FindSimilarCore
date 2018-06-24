namespace FindSimilarServices.CSCore.Codecs.ADPCM
{
    // see https://github.com/jwzhangjie/Adpcm_Pcm/blob/master/Adpcm.c
    // https://gist.github.com/jaames/c837fb87a6a5585d47baa1b8e2408234
    // https://github.com/srnsw/xena/blob/master/plugins/audio/ext/src/tritonus/src/classes/org/tritonus/sampled/convert/ImaAdpcmFormatConversionProvider.java
    // https://github.com/lguipeng/TwoWayRadio/blob/master/app/src/main/java/com/szu/twowayradio/utils/Adpcm.java

    /*
    ** Intel/DVI Adpcm coder/decoder.
    **
    ** The algorithm for this coder was taken from the IMA Compatability Project
    ** proceedings, Vol 2, Number 2; May 1992.
    **
    ** Version 1.2, 18-Dec-92.
    **
    ** Change log:
    ** - Fixed a stupid bug, where the delta was computed as
    **   stepsize*code/4 in stead of stepsize*(code+0.5)/4.
    ** - There was an off-by-one error causing it to pick
    **   an incorrect delta once in a blue moon.
    ** - The NODIVMUL define has been removed. Computations are now always done
    **   using shifts, adds and subtracts. It turned out that, because the standard
    **   is defined using shift/add/subtract, you needed bits of fixup code
    **   (because the div/mul simulation using shift/add/sub made some rounding
    **   errors that real div/mul don't make) and all together the resultant code
    **   ran slower than just using the shifts all the time.
    ** - Changed some of the variable names to be more meaningful.
    */

    /* modefied by juguofeng<jgfntu@163.com> 2012-05-20 */
    public class AdpcmState
    {
        public int ValuePredicted
        {
            get; set;
        }
        public int Index
        {
            get; set;
        }
    }

    public class Adpcm
    {
        private bool isBigEndian = false;

        public Adpcm(bool isBigEndian = false)
        {
            this.isBigEndian = isBigEndian;
        }

        static int[] indexTable =
        {
            -1, -1, -1, -1, 2, 4, 6, 8,
            -1, -1, -1, -1, 2, 4, 6, 8,
            };

        static int[] stepsizeTable =
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

        public int AdpcmCoder(byte[] inBuffer, byte[] outBuffer, int outByteOffset, int inFrameCount, AdpcmState state)
        {
            int inp;        /* Input buffer pointer */
            int outp;       /* output buffer pointer */
            int val;        /* Current input sample value */
            int sign;       /* Current Adpcm sign bit */
            int delta;      /* Current Adpcm output value */
            int diff;       /* Difference between val and valprev */
            int step;       /* Stepsize */
            int valpred;    /* Predicted output value */
            int vpdiff;     /* Current change to valpred */
            int index;      /* Current step change index */
            int outputbuffer = 0;   /* place to keep previous 4-bit value */
            bool bufferstep;    /* toggle between outputbuffer/output */
            int len = inFrameCount;

            inp = 0;
            outp = outByteOffset;

            valpred = state.ValuePredicted;
            index = state.Index;
            step = stepsizeTable[index];

            bufferstep = true;

            for (; len > 0; len--)
            {
                val = isBigEndian ?
                    ((inBuffer[inp] << 8) | (inBuffer[inp + 1] & 0xFF)) :
                    ((inBuffer[inp + 1] << 8) | (inBuffer[inp] & 0xFF));
                inp += 2;

                /* Step 1 - compute difference with previous value */
                diff = val - valpred;
                sign = (diff < 0) ? 8 : 0;
                if (sign != 0)
                    diff = (-diff);

                /* Step 2 - Divide and clamp */
                /* Note:
				** This code *approximately* computes:
				**    delta = diff*4/step;
				**    vpdiff = (delta+0.5)*step/4;
				** but in shift step bits are dropped. The net result of this is
				** that even if you have fast mul/div hardware you cannot Put it to
				** good use since the fixup would be too expensive.
				*/
                delta = 0;
                vpdiff = (step >> 3);

                if (diff >= step)
                {
                    delta = 4;
                    diff -= step;
                    vpdiff += step;
                }
                step >>= 1;
                if (diff >= step)
                {
                    delta |= 2;
                    diff -= step;
                    vpdiff += step;
                }
                step >>= 1;
                if (diff >= step)
                {
                    delta |= 1;
                    vpdiff += step;
                }

                /* Step 3 - Update previous value */
                if (sign != 0)
                    valpred -= vpdiff;
                else
                    valpred += vpdiff;

                /* Step 4 - Clamp previous value to 16 bits */
                if (valpred > 32767)
                    valpred = 32767;
                else if (valpred < -32768)
                    valpred = -32768;

                /* Step 5 - Assemble value, update index and step values */
                delta |= sign;

                index += indexTable[delta];
                if (index < 0)
                    index = 0;
                if (index > 88)
                    index = 88;
                step = stepsizeTable[index];

                /* Step 6 - Output value */
                if (bufferstep)
                {
                    outputbuffer = (delta << 4) & 0xf0;
                }
                else
                {
                    outBuffer[outp++] = (byte)((delta & 0x0f) | outputbuffer);
                }
                bufferstep = !bufferstep;
            }

            /* Output last step, if needed */
            if (!bufferstep)
                outBuffer[outp++] = (byte)outputbuffer;

            state.ValuePredicted = valpred;
            state.Index = index;
            return inFrameCount;
        }

        public int AdpcmDecoder(byte[] inBuffer, byte[] outBuffer, int outByteOffset, int inFrameCount, AdpcmState state)
        {
            int inp;        /* Input buffer pointer */
            int outp;       /* output buffer pointer */
            int sign;       /* Current Adpcm sign bit */
            int delta;      /* Current Adpcm output value */
            int step;       /* Stepsize */
            int valpred;    /* Predicted value */
            int vpdiff;     /* Current change to valpred */
            int index;      /* Current step change index */
            int inputbuffer = 0;    /* place to keep next 4-bit value */
            bool bufferstep;    /* toggle between inputbuffer/input */
            int len = inFrameCount;

            inp = 0;
            outp = outByteOffset;

            valpred = state.ValuePredicted;
            index = state.Index;
            step = stepsizeTable[index];

            bufferstep = false;

            for (; len > 0; len--)
            {
                /* Step 1 - Get the delta value */
                if (bufferstep)
                {
                    delta = inputbuffer & 0xf;
                }
                else
                {
                    inputbuffer = inBuffer[inp];
                    inp++;
                    delta = (inputbuffer >> 4) & 0xf;
                }
                bufferstep = !bufferstep;

                /* Step 2 - Find new index value (for later) */
                index += indexTable[delta];
                if (index < 0) index = 0;
                if (index > 88) index = 88;

                /* Step 3 - Separate sign and magnitude */
                sign = delta & 8;
                delta = delta & 7;

                /* Step 4 - Compute difference and new predicted value */
                /*
				** Computes 'vpdiff = (delta+0.5)*step/4', but see comment
				** in Adpcm_coder.
				*/
                vpdiff = step >> 3;
                if ((delta & 4) != 0)
                    vpdiff += step;
                if ((delta & 2) != 0)
                    vpdiff += step >> 1;
                if ((delta & 1) != 0)
                    vpdiff += step >> 2;

                if (sign != 0)
                    valpred -= vpdiff;
                else
                    valpred += vpdiff;

                /* Step 5 - clamp output value */
                if (valpred > 32767)
                    valpred = 32767;
                else if (valpred < -32768)
                    valpred = -32768;

                /* Step 6 - Update step value */
                step = stepsizeTable[index];

                /* Step 7 - Output value */
                if (isBigEndian)
                {
                    outBuffer[outp++] = (byte)(valpred >> 8);
                    outBuffer[outp++] = (byte)(valpred & 0xFF);
                }
                else
                {
                    outBuffer[outp++] = (byte)(valpred & 0xFF);
                    outBuffer[outp++] = (byte)(valpred >> 8);
                }
            }

            state.ValuePredicted = valpred;
            state.Index = index;
            return inFrameCount;
        }
    }
}