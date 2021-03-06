using System;
using System.Drawing;
using System.Drawing.Imaging;
using Xunit;
using FindSimilarServices;
using FindSimilarServices.Audio;
using SoundFingerprinting;
using SoundFingerprinting.Audio;
using SoundFingerprinting.SoundTools.DrawingTool;
using CommonUtils.Audio;
using System.IO;

namespace SoundFingerprinting
{
    public class SoundFingerprintingTests
    {
        [Fact]
        public void Test1()
        {
            // https://github.com/AddictedCS/soundfingerprinting.soundtools/blob/master/src/SoundFingerprinting.SoundTools/DrawningTool/WinDrawningTool.cs

            string pathToSourceFile = @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\Clean Bandit - Rather Be Programming\Clean Bandit - Region 1 Slow.wav";
            const int sampleRate = 32000;
            string DirectoryPath = @"C:\Users\pnerseth\My Projects\test-output";

            var audioService = new FindSimilarAudioService();
            AudioSamples data = audioService.ReadMonoSamplesFromFile(pathToSourceFile, sampleRate, 2, 5);

/* 
            var imageService = new FindSimilarImageService();
            using (Image image = imageService.GetSignalImage(data.Samples, 2000, 500))
            {
                image.Save(Path.Combine(DirectoryPath, "_downsampled.png"), ImageFormat.Jpeg);
            }

 */
            SoundIO.WriteWaveFile(Path.Combine(DirectoryPath, "_resampled.wav"), data.Samples, sampleRate);

        }
    }
}
