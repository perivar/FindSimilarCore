using System;
using System.Drawing;
using System.Drawing.Imaging;
using Xunit;
using FindSimilarServices;
using Serilog;
using FindSimilarServices.Audio;
using CommonUtils.Audio;
using System.IO;

namespace FindSimilarTest
{
    public class AudioFixture : IDisposable
    {
        public AudioFixture()
        {
            // sox command to test failed audio files
            // sox --i -V6 "FILE"

            FindSimilarAudioService = new FindSimilarAudioService();
            SampleRate = 32000;
            DirectoryPath = @"C:\Users\pnerseth\My Projects\test-output";

            // create if it doesn't exist
            if (!Directory.Exists(DirectoryPath))
            {
                Directory.CreateDirectory(DirectoryPath);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .CreateLogger();

        }
        public void Dispose()
        {
            FindSimilarAudioService = null;
        }

        public FindSimilarAudioService FindSimilarAudioService { get; private set; }

        public int SampleRate { get; private set; }

        public string DirectoryPath { get; private set; }
    }

    public class CSCoreTests : IClassFixture<AudioFixture>
    {
        AudioFixture fixture;

        public CSCoreTests(AudioFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public void TestWav24Files()
        {
            // Wav 24 bit extensible format
            ReadAndWrite("WAV 24 bit extensible format",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Preset_Template\HTMEM-Another-Walkthrough-To-An-EDM-Beat\mhak kick 209 G#.wav",
            Path.Combine(fixture.DirectoryPath, "wav-24bit-extensible.wav"));

            // 24 bit PCM files
            ReadAndWrite("WAV 24 bit PCM files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\House Baerum\ATE Reverb Kick - 003.wav",
            Path.Combine(fixture.DirectoryPath, "wav-24bit-pcm.wav"));
        }

        [Fact]
        public void TestMuLawFiles()
        {
            // MuLaw files
            ReadAndWrite("MuLaw files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Ed Sheeran - Shape Of You\Snare (2).wav",
            Path.Combine(fixture.DirectoryPath, "wav-mulaw-1.wav"));

            ReadAndWrite("MuLaw files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Preset_Template\FSS Essential Collection Vol 2\Kraftwerk - The Robots (Album Mix)\cow bell001.wav",
            Path.Combine(fixture.DirectoryPath, "wav-mulaw-2.wav"));

            fixture.FindSimilarAudioService.ConvertToPCM16Bit(
                @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Preset_Template\FSS Essential Collection Vol 2\Kraftwerk - The Robots (Album Mix)\cow bell001.wav",
                Path.Combine(fixture.DirectoryPath, "wav-mulaw-2-original.wav"));

            ReadAndWrite("MuLaw files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Rihanna - We Found Love ft. Calvin Harris by www.aronize.tk\DNC_Hat_4.wav",
            Path.Combine(fixture.DirectoryPath, "wav-mulaw-3.wav"));
        }

        [Fact]
        public void TestOggFiles()
        {
            // OGG test files
            ReadAndWrite("Ogg files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Avicii - Silhouettes (Melody Remake by EtasDj)\Sweep 1.ogg",
            Path.Combine(fixture.DirectoryPath, "ogg-1.wav"));

            ReadAndWrite("Ogg files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Avicii - Silhouettes (Melody Remake by EtasDj)\Crash 2.ogg",
            Path.Combine(fixture.DirectoryPath, "ogg-2.wav"));

            ReadAndWrite("Ogg files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\Jason Derulo In My Head Remix\La Manga.ogg",
            Path.Combine(fixture.DirectoryPath, "ogg-3.wav"));

            // OGG within wav container fest files
            ReadAndWrite("Ogg within WAV container files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Van Halen Jump\FPC_Crash_G16InLite_01.wav",
            Path.Combine(fixture.DirectoryPath, "wav-ogg-1.wav"));

            ReadAndWrite("Ogg within WAV container files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Tutorials\Electro Dance tutorial by Phil Doon\DNC_Kick.wav",
            Path.Combine(fixture.DirectoryPath, "wav-ogg-2.wav"));

            ReadAndWrite("Ogg within WAV container files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!Preset_Template\FSS Essential Collection Vol 1\Tobias Davy - Psychedelic House (Original Mix)\DNC_Clap_2.wav",
            Path.Combine(fixture.DirectoryPath, "wav-ogg-3.wav"));
        }

        [Fact]
        public void TestMP3Files()
        {
            ReadAndWrite("mp3 file that fails due to frame errors (quantization error)",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Britney Spears - 3\03 Britney Spears - 3 (Acapella).mp3",
            Path.Combine(fixture.DirectoryPath, "mp3-quantizise-error.wav"));
        }

        [Fact]
        public void TestWavSpecialChunksFiles()
        {
            // wav files with LIST chunk            
            ReadAndWrite("wav file with LIST chunk error",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Poker Face\FPC_Crash_G16InLite_05.wav",
            Path.Combine(fixture.DirectoryPath, "wav-list-chunk-error-1.wav"));

            ReadAndWrite("wav file with LIST chunk error",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\Clean Bandit - Rather Be Programming\Clean Bandit - Region 1 Slow.wav",
            Path.Combine(fixture.DirectoryPath, "wav-list-chunk-error-2.wav"));

            // wav files with PAD sub chunk
            ReadAndWrite("wav file with PAD chunk error",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Black Eyed Peas - The Time Mehran Abbasi Reworked Final/VEH3 Snares 026.wav",
            Path.Combine(fixture.DirectoryPath, "wav-pad-chunk-error-1.wav"));

            ReadAndWrite("wav file with PAD chunk error",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\Lynda EDM Drums\Crash.wav",
            Path.Combine(fixture.DirectoryPath, "wav-pad-chunk-error-2.wav"));
        }

        [Fact]
        public void TestADPCMFiles()
        {

            // ADPCM test files
            ReadAndWrite("WAV ADPCM files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Marry The Night (Afrojack Remix) Leo Villagra Remake\Yeah fxvoice afrpck 16.wav",
            Path.Combine(fixture.DirectoryPath, "wav-adpcm-1.wav"));

            ReadAndWrite("WAV ADPCM files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Marry The Night (Afrojack Remix) Leo Villagra Remake\bass afrpck 8.wav",
            Path.Combine(fixture.DirectoryPath, "wav-adpcm-2.wav"));

            ReadAndWrite("WAV ADPCM files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Lady Gaga - Marry The Night (Afrojack Remix) Leo Villagra Remake\clap afrpck 5.wav",
            Path.Combine(fixture.DirectoryPath, "wav-adpcm-3.wav"));

            ReadAndWrite("WAV ADPCM files",
            @"C:\Users\pnerseth\Documents\Audacity\bass afrpck 8 - fix.wav",
            Path.Combine(fixture.DirectoryPath, "wav-adpcm-4.wav"));

            ReadAndWrite("WAV ADPCM files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\Test Files\snare-ms-adpcm.wav",
            Path.Combine(fixture.DirectoryPath, "wav-adpcm-5.wav"));

            ReadAndWrite("WAV ADPCM files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\Test Files\snare-ima-adpcm.wav",
            Path.Combine(fixture.DirectoryPath, "wav-adpcm-6.wav"));

            // long adpcm file
            ReadAndWrite("WAV ADPCM files",
            @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Timbaland ft One republic - Apologize\STR_3c_Long.wav",
            Path.Combine(fixture.DirectoryPath, "wav-adpcm-long-1.wav"));

        }

        private void ReadAndWrite(string category, string audioPath, string outputPath)
        {
            var audioData = fixture.FindSimilarAudioService.ReadMonoSamplesFromFile(audioPath, fixture.SampleRate, 0, 0);
            SoundIO.WriteWaveFile(outputPath, audioData.Samples, fixture.SampleRate);
        }
    }
}