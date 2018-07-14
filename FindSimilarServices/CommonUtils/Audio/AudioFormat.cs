using System;
using System.Text;
using CSCore;

namespace CommonUtils.Audio
{
    // http://www-mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html
    public class AudioFormat : ICloneable, IEquatable<AudioFormat>
    {
        private AudioEncoding _encoding;
        private short _channels;
        private int _sampleRate;
        private int _averageBytesPerSecond;
        private int _blockAlign;
        private short _bitsPerSample;
        private short _extraSize;
        private int _samplesPerBlock;
        private int _bytesPerBlock;
        private short _coefficients;
        private int _samplesPerChannel;
        private long _dataChunkSize;
        private long _dataStartPosition;
        private long _dataEndPosition;
        private int _numberOfValidBits;
        private uint _speakerPositionMask;
        private AudioEncoding _subEncoding;

        /// <summary>
        /// Gets the number of channels in the waveform-audio data. Mono data uses one channel and stereo data uses two
        /// channels.
        /// </summary>
        public virtual int Channels
        {
            get { return _channels; }
            protected internal set
            {
                _channels = (short)value;
            }
        }

        /// <summary>
        /// Gets the sample rate, in samples per second (hertz).
        /// </summary>
        public virtual int SampleRate
        {
            get { return _sampleRate; }
            protected internal set
            {
                _sampleRate = value;
            }
        }

        /// <summary>
        /// Gets the required average data transfer rate, in bytes per second. For example, 16-bit stereo at 44.1 kHz has an
        /// average data rate of 176,400 bytes per second (2 channels — 2 bytes per sample per channel — 44,100 samples per
        /// second).
        /// </summary>
        public virtual int AverageBytesPerSecond
        {
            get { return _averageBytesPerSecond; }
            protected internal set
            {
                _averageBytesPerSecond = value;
            }
        }

        /// <summary>
        /// Gets the block alignment, in bytes. The block alignment is the minimum atomic unit of data. For PCM data, the block
        /// alignment is the number of bytes used by a single sample, including data for both channels if the data is stereo.
        /// For example, the block alignment for 16-bit stereo PCM is 4 bytes (2 channels x 2 bytes per sample).
        /// </summary>
        public virtual int BlockAlign
        {
            get { return _blockAlign; }
            protected internal set
            {
                _blockAlign = (short)value;
            }
        }

        /// <summary>
        /// Gets the number of bits, used to store one sample.
        /// </summary>
        public virtual int BitsPerSample
        {
            get { return _bitsPerSample; }
            protected internal set
            {
                _bitsPerSample = (short)value;
            }
        }

        /// <summary>
        /// Gets the size (in bytes) of extra information.
        /// </summary>
        public virtual int ExtraSize
        {
            get { return _extraSize; }
            protected internal set
            {
                _extraSize = (short)value;
            }
        }

        /// <summary>
        /// Get number of samples per block
        /// Used by ADPCM formats
        /// </summary>
        public virtual int SamplesPerBlock
        {
            get { return _samplesPerBlock; }
            protected internal set
            {
                _samplesPerBlock = value;
            }
        }

        /// <summary>
        /// Get number of bytes per block
        /// Used by ADPCM formats
        /// </summary>
        public virtual int BytesPerBlock
        {
            get { return _bytesPerBlock; }
            protected internal set
            {
                _bytesPerBlock = value;
            }
        }

        /// <summary>
        /// Get number of coefficients
        /// Used by ADPCM formats
        /// </summary>
        public virtual int Coefficients
        {
            get { return _coefficients; }
            protected internal set
            {
                _coefficients = (short)value;
            }
        }

        /// <summary>
        /// Get number of samples per channel
        /// Used by ADPCM formats
        /// </summary>
        public virtual int SamplesPerChannel
        {
            get { return _samplesPerChannel; }
            protected internal set
            {
                _samplesPerChannel = value;
            }
        }

        /// <summary>
        /// Gets the number of data bytes
        /// </summary>
        public virtual long DataChunkSize
        {
            get { return _dataChunkSize; }
            protected internal set
            {
                _dataChunkSize = value;
            }
        }

        /// <summary>
        /// Gets the data chunk start position
        /// </summary>
        public virtual long DataStartPosition
        {
            get { return _dataStartPosition; }
            protected internal set
            {
                _dataStartPosition = value;
            }
        }

        /// <summary>
        /// Gets the data chunk end position
        /// </summary>
        public virtual long DataEndPosition
        {
            get { return _dataEndPosition; }
            protected internal set
            {
                _dataEndPosition = value;
            }
        }


        /// <summary>
        /// Return the number of valid bits, used for the 
        /// Used by the Wav Extensible format
        /// </summary>
        /// <value></value>
        public virtual int NumberOfValidBits
        {
            get { return _numberOfValidBits; }
            protected internal set
            {
                _numberOfValidBits = value;
            }
        }


        /// <summary>
        /// The loudspeaker position mask uses 18 bits, each bit corresponding to a speaker position 
        /// (e.g. Front Left or Top Back Right), to indicate the channel to speaker mapping. 
        /// An all-zero field indicates that channels are mapped to outputs in order: 
        /// first channel to first output, second channel to second output, etc.
        /// Order |  Bit  | Channel
        /// 1.        0x1 Front Left
        /// 2.        0x2 Front Right
        /// 3.        0x4 Front Center
        /// 4.        0x8 Low Frequency (LFE)
        /// 5.       0x10 Back Left (Surround Back Left)
        /// 6.       0x20 Back Right (Surround Back Right)
        /// 7.       0x40 Front Left of Center
        /// 8.       0x80 Front Right of Center
        /// 9.      0x100 Back Center
        /// 10.      0x200 Side Left (Surround Left)
        /// 11.      0x400 Side Right (Surround Right)
        /// 12.      0x800 Top Center
        /// 13.     0x1000 Top Front Left
        /// 14.     0x2000 Top Front Center
        /// 15.     0x4000 Top Front Right
        /// 16.     0x8000 Top Back Left
        /// 17.    0x10000 Top Back Center
        /// 18.    0x20000 Top Back Right
        /// Used by the Wav Extensible format
        ///</summary>
        public virtual uint SpeakerPositionMask
        {
            get { return _speakerPositionMask; }
            protected internal set
            {
                _speakerPositionMask = value;
            }
        }

        /// <summary>
        /// Gets the waveform-audio format type.
        /// </summary>
        public virtual AudioEncoding Encoding
        {
            get { return _encoding; }
            protected internal set
            {
                _encoding = value;
            }
        }

        /// <summary>
        /// Gets the waveform-audio sub-format type.
        /// Used by the Wav Extensible format
        /// </summary>
        public virtual AudioEncoding SubEncoding
        {
            get { return _subEncoding; }
            protected internal set
            {
                _subEncoding = value;
            }
        }

        /// <summary>
        /// Creates a new 32 bit IEEE floating point wave format
        /// </summary>
        /// <param name="sampleRate">sample rate</param>
        /// <param name="channels">number of channels</param>
        public static AudioFormat CreateIeeeFloaAudioFormat(int sampleRate, int channels)
        {
            var aF = new AudioFormat();
            aF.Encoding = AudioEncoding.IeeeFloat;
            aF.Channels = (short)channels;
            aF.BitsPerSample = 32;
            aF.SampleRate = sampleRate;
            aF.BlockAlign = (short)(4 * channels);
            aF.AverageBytesPerSecond = sampleRate * aF.BlockAlign;
            aF.ExtraSize = 0;
            return aF;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">The <see cref="AudioFormat"/> to compare with this <see cref="AudioFormat"/>.</param>
        /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
        public virtual bool Equals(AudioFormat other)
        {
            return Channels == other.Channels &&
                   SampleRate == other.SampleRate &&
                   AverageBytesPerSecond == other.AverageBytesPerSecond &&
                   BlockAlign == other.BlockAlign &&
                   BitsPerSample == other.BitsPerSample &&
                   ExtraSize == other.ExtraSize &&
                   Encoding == other.Encoding;
        }

        /// <summary>
        /// Creates a new <see cref="AudioFormat" /> object that is a copy of the current instance.
        /// </summary>
        /// <returns>A copy of the current instance.</returns>
        public virtual object Clone()
        {
            return MemberwiseClone(); //since there are value types MemberWiseClone is enough.
        }

        /// <summary>
        ///     Returns a string which describes the <see cref="AudioFormat" />.
        /// </summary>
        /// <returns>A string which describes the <see cref="AudioFormat" />.</returns>
        public override string ToString()
        {
            return GetInformation().ToString();
        }

        private StringBuilder GetInformation()
        {
            var builder = new StringBuilder();
            builder.AppendFormat("Reading Wave file: {0} format, {1} channels, {2} samp/sec", _encoding, _channels, _sampleRate);
            builder.AppendLine();
            builder.AppendFormat("{0} byte/sec, {1} block align, {2} bits/samp, {3} data bytes", _averageBytesPerSecond, _blockAlign, _bitsPerSample, _dataChunkSize);
            builder.AppendLine();
            builder.AppendFormat("{0} Extsize", _extraSize);
            builder.AppendFormat(", {0} Samps/block", _samplesPerBlock);
            builder.AppendFormat(", {0} bytes/block", _bytesPerBlock);
            if (_coefficients > 0) builder.AppendFormat(", {0} Num Coefs", _coefficients);
            builder.AppendFormat(", {0} Samps/chan", _samplesPerChannel);
            builder.AppendLine();
            return builder;
        }
    }
}