using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using CommonUtils;
using SoundFingerprinting.Data;
using SoundFingerprinting.Wavelets;

namespace SoundFingerprinting.SoundTools.DrawningTool
{
    internal class ImageService
    {
        private const int PixelsBetweenImages = 10;

        private readonly IWaveletDecomposition waveletDecomposition;

        public ImageService()
            : this(new StandardHaarWaveletDecomposition())
        {
        }

        public ImageService(IWaveletDecomposition waveletDecomposition)
        {
            this.waveletDecomposition = waveletDecomposition;
        }

        public Image GetImageForFingerprint(Fingerprint data, int width, int height)
        {
            Bitmap image = new Bitmap(width, height, PixelFormat.Format16bppRgb565);
            DrawFingerprintInImage(image, data.Signature.ToBools(), width, height, 0, 0);
            return image;
        }

        public Image GetImageForFingerprints(List<Fingerprint> fingerprints, int width, int height, int imagesPerRow)
        {
            int fingersCount = fingerprints.Count;
            int rowCount = (int)System.Math.Ceiling((float)fingersCount / imagesPerRow);
            int imageWidth = (imagesPerRow * (width + PixelsBetweenImages)) + PixelsBetweenImages;
            int imageHeight = (rowCount * (height + PixelsBetweenImages)) + PixelsBetweenImages;

            Bitmap image = new Bitmap(imageWidth, imageHeight, PixelFormat.Format16bppRgb565);
            SetBackground(image, Color.White);

            int verticalOffset = PixelsBetweenImages;
            int horizontalOffset = PixelsBetweenImages;
            int count = 0;
            foreach (var fingerprint in fingerprints)
            {
                DrawFingerprintInImage(image, fingerprint.Signature.ToBools(), width, height, horizontalOffset, verticalOffset);
                count++;
                if (count % imagesPerRow == 0)
                {
                    verticalOffset += height + PixelsBetweenImages;
                    horizontalOffset = PixelsBetweenImages;
                }
                else
                {
                    horizontalOffset += width + PixelsBetweenImages;
                }
            }

            return image;
        }

        public Image GetSignalImage(float[] data, int width, int height)
        {
            Bitmap image = new Bitmap(width, height);
            Graphics graphics = Graphics.FromImage(image);

            FillBackgroundColor(width, height, graphics, Color.Black);
            DrawGridlines(width, height, graphics);

            int center = height / 2;
            /*Draw lines*/
            using (Pen pen = new Pen(Color.MediumSpringGreen, 1))
            {
                /*Find delta X, by which the lines will be drawn*/
                double deltaX = (double)width / data.Length;
                double normalizeFactor = data.Max(a => System.Math.Abs(a)) / ((double)height / 2);
                for (int i = 0, n = data.Length; i < n; i++)
                {
                    graphics.DrawLine(
                        pen,
                        (float)(i * deltaX),
                        center,
                        (float)(i * deltaX),
                        (float)(center - (data[i] / normalizeFactor)));
                }
            }

            using (Pen pen = new Pen(Color.DarkGreen, 1))
            {
                /*Draw center line*/
                graphics.DrawLine(pen, 0, center, width, center);
            }

            DrawCopyrightInfo(graphics, 10, 10);
            return image;
        }

		public Image GetSpectrogramImage(float[] spectrum, int width, int logBins, int drawWidth=2000, int drawHeight=400)
		{
            float[][] frames = new float[width][];
            for (int i = 0; i < width; i++) {
                float[] band = new float[logBins];
                for (int j = 0; j < logBins; j++) {
                    band[j] = spectrum[logBins * i + j];
                }
                frames[i] = band; 
            }

            return  GetSpectrogramImage(frames, drawWidth, drawHeight);
        }

		public Image GetSpectrogramImage(float[][] spectrum, int width, int height)
		{
			// set some default values
			bool usePowerSpectrum = false;
			bool colorize = true;
			bool flipYscale = true;
			int forceWidth = width;
			int forceHeight = height;
			
			// amplitude (or magnitude) is the square root of the power spectrum
			// the magnitude spectrum is abs(fft), i.e. Math.Sqrt(re*re + img*img)
			// use 20*log10(Y) to get dB from amplitude
			// the power spectrum is the magnitude spectrum squared
			// use 10*log10(Y) to get dB from power spectrum
			double maxValue = spectrum.Max((b) => b.Max((v) => System.Math.Abs(v)));
			if (usePowerSpectrum) {
				maxValue = 10 * System.Math.Log10(maxValue);
			} else {
				maxValue = 20 * System.Math.Log10(maxValue);
			}
			
			if (maxValue == 0.0f)
				return null;

			int blockSizeX = 1;
			int blockSizeY = 1;
			
			int rowCount = spectrum[0].Length;
			int columnCount = spectrum.Length;
			
			Bitmap img = new Bitmap(columnCount*blockSizeX, rowCount*blockSizeY);
			Graphics graphics = Graphics.FromImage(img);
			
			for(int column = 0; column < columnCount; column++)
			{
				for(int row = 0; row < rowCount; row++)
				{
					double val = spectrum[column][row];
					if (usePowerSpectrum) {
						val = 10 * System.Math.Log10(val);
					} else {
						val = 20 * System.Math.Log10(val);
					}
					
					Color color = ColorUtils.ValueToBlackWhiteColor(val, maxValue);
					Brush brush = new SolidBrush(color);
					
					if (flipYscale) {
						// draw a small square
						graphics.FillRectangle(brush, column*blockSizeX, (rowCount-row-1)*blockSizeY, blockSizeX, blockSizeY);
					} else {
						// draw a small square
						graphics.FillRectangle(brush, column*blockSizeX, row*blockSizeY, blockSizeX, blockSizeY);
					}
				}
			}
			
			// Should we resize?
			if (forceHeight > 0 && forceWidth > 0) {
				img = (Bitmap) ImageUtils.Resize(img, forceWidth, forceHeight, false);
			}
			
			// Should we colorize?
			if (colorize) img = ColorUtils.Colorize(img, 255, ColorUtils.ColorPaletteType.MATLAB);

			return img;
		}

        public Image GetSpectrogramImageOriginal(float[][] spectrum, int width, int height)
        {
            Bitmap image = new Bitmap(width, height);
            Graphics graphics = Graphics.FromImage(image);
            FillBackgroundColor(width, height, graphics, Color.Black);

            int bands = spectrum[0].Length;
            double deltaX = (double)(width - 1) / spectrum.Length; /*By how much the image will move to the left*/
            double deltaY = (double)(height - 1) / (bands + 1); /*By how much the image will move upward*/
            int prevX = 0;
            for (int i = 0, n = spectrum.Length; i < n; i++)
            {
                double x = i * deltaX;
                if ((int)x == prevX)
                {
                    continue;
                }

                for (int j = 0, m = spectrum[0].Length; j < m; j++)
                {
                    Color color;
                    if (j == (int)(318 / (5512.0f / 2 / spectrum[0].Length)) || j == (int)(2000 / (5512.0f / 2 / spectrum[0].Length)))
                    {
                        color = Color.Red;
                    }
                    else
                    {
                        color = ValueToBlackWhiteColor(spectrum[i][j]);
                    }

                    image.SetPixel((int)x, height - (int)(deltaY * j) - 1, color);
                }

                prevX = (int)x;
            }

            DrawCopyrightInfo(graphics, 10, 10);
            return image;
        }

        public Image GetLogSpectralImages(List<SpectralImage> spectralImages, int imagesPerRow)
        {
            int width = spectralImages[0].Rows;
            int height = spectralImages[0].Cols;
            int fingersCount = spectralImages.Count;
            int rowCount = (int)System.Math.Ceiling((float)fingersCount / imagesPerRow);
            int imageWidth = (imagesPerRow * (width + PixelsBetweenImages)) + PixelsBetweenImages;
            int imageHeight = (rowCount * (height + PixelsBetweenImages)) + PixelsBetweenImages;
            Bitmap image = new Bitmap(imageWidth, imageHeight, PixelFormat.Format16bppRgb565);

            SetBackground(image, Color.White);

            int verticalOffset = PixelsBetweenImages;
            int horizontalOffset = PixelsBetweenImages;
            int count = 0;
            foreach (float[][] spectralImage in spectralImages.Select(im => Transform2D(im)))
            {
                for (int i = 0; i < width /*128*/; i++)
                {
                    for (int j = 0; j < height /*32*/; j++)
                    {
                        Color color = ValueToBlackWhiteColor(spectralImage[i][j]);
                        image.SetPixel(i + horizontalOffset, j + verticalOffset, color);
                    }
                }

                count++;
                if (count % imagesPerRow == 0)
                {
                    verticalOffset += height + PixelsBetweenImages;
                    horizontalOffset = PixelsBetweenImages;
                }
                else
                {
                    horizontalOffset += width + PixelsBetweenImages;
                }
            }

            return image;
        }

        private float[][] Transform2D(SpectralImage spectralImage)
        {
            float[][] transformed = new float[spectralImage.Rows][];

            for (int i = 0; i < spectralImage.Rows; ++i)
            {
                transformed[i] = new float[spectralImage.Cols];
                Buffer.BlockCopy(spectralImage.Image, i * spectralImage.Cols * sizeof(float), transformed[i], 0, spectralImage.Cols * sizeof(float));
            }

            return transformed;
        }

        public Image GetWaveletsImages(List<SpectralImage> spectralImages, int imagesPerRow, double haarWaveletNorm)
        {

            foreach (var spectralImage in spectralImages)
            {
                waveletDecomposition.DecomposeImageInPlace(spectralImage.Image, spectralImage.Rows, spectralImage.Cols, haarWaveletNorm);
            }

            int width = spectralImages[0].Rows;
            int height = spectralImages[0].Cols;
            int fingersCount = spectralImages.Count;
            int rowCount = (int)System.Math.Ceiling((float)fingersCount / imagesPerRow);
            int imageWidth = (imagesPerRow * (width + PixelsBetweenImages)) + PixelsBetweenImages;
            int imageHeight = (rowCount * (height + PixelsBetweenImages)) + PixelsBetweenImages;
            Bitmap image = new Bitmap(imageWidth, imageHeight, PixelFormat.Format16bppRgb565);

            SetBackground(image, Color.White);

            int verticalOffset = PixelsBetweenImages;
            int horizontalOffset = PixelsBetweenImages;
            int count = 0;
            foreach (float[][] spectralImage in spectralImages.Select(Transform2D))
            {
                for (int i = 0; i < width /*128*/; i++)
                {
                    for (int j = 0; j < height /*32*/; j++)
                    {
                        Color color = ValueToBlackWhiteColor(spectralImage[i][j]);
                        image.SetPixel(i + horizontalOffset, j + verticalOffset, color);
                    }
                }

                count++;
                if (count % imagesPerRow == 0)
                {
                    verticalOffset += height + PixelsBetweenImages;
                    horizontalOffset = PixelsBetweenImages;
                }
                else
                {
                    horizontalOffset += width + PixelsBetweenImages;
                }
            }

            return image;
        }

        public Image GetWaveletTransformedImage(float[][] image, IWaveletDecomposition wavelet, double haarWaveletNorm)
        {
            int width = image[0].Length;
            int height = image.Length;

            float[] transformedImages = new float[width * height];

            for (int i = 0; i < height; ++i)
            {
                for (int j = 0; j < width; ++j)
                {
                    transformedImages[i * width + j] = image[i][j];
                }
            }

            wavelet.DecomposeImageInPlace(transformedImages, height, width, haarWaveletNorm);

            Bitmap transformed = new Bitmap(width, height, PixelFormat.Format16bppRgb565);
            for (int i = 0; i < transformed.Height; i++)
            {
                for (int j = 0; j < transformed.Width; j++)
                {
                    transformed.SetPixel(j, i, Color.FromArgb((int)image[i][j]));
                }
            }

            return transformed;
        }

        private Color InverseToBlackWhite(double value)
        {
            double abs = System.Math.Abs(value); // after wavelet transformation this value will be [-1, 1]
            int color = 255 - System.Math.Min((int)(abs * 255), 255);
            return Color.FromArgb(color, color, color);
        }

        private Color ValueToBlackWhiteColor(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) {
                value = 0.0;
            }

            double abs = System.Math.Abs(value); // after wavelet transformation this value will be [-1, 1]
            int color = System.Math.Min((int)(abs * 255), 255);
            return Color.FromArgb(color, color, color);
        }

        private void FillBackgroundColor(int width, int height, Graphics graphics, Color color)
        {
            using (Brush brush = new SolidBrush(color))
            {
                graphics.FillRectangle(brush, new Rectangle(0, 0, width, height));
            }
        }

        private void DrawCopyrightInfo(Graphics graphics, int x, int y)
        {
            FontFamily fontFamily = new FontFamily("Courier New");
            Font font = new Font(fontFamily, 10);
            Brush textbrush = Brushes.White;
            Point coordinate = new Point(x, y);
            graphics.DrawString("https://github.com/AddictedCS/soundfingerprinting", font, textbrush, coordinate);
        }

        private void DrawGridlines(int width, int height, Graphics graphics)
        {
            const int Gridline = 50; /*Every 50 pixels draw gridline*/
            /*Draw gridlines*/
            using (Pen pen = new Pen(Color.Red, 1))
            {
                /*Draw horizontal gridlines*/
                for (int i = 1; i < height / Gridline; i++)
                {
                    graphics.DrawLine(pen, 0, i * Gridline, width, i * Gridline);
                }

                /*Draw vertical gridlines*/
                for (int i = 1; i < width / Gridline; i++)
                {
                    graphics.DrawLine(pen, i * Gridline, 0, i * Gridline, height);
                }
            }
        }

        private void DrawFingerprintInImage(
            Bitmap image, bool[] fingerprint, int fingerprintWidth, int fingerprintHeight, int xOffset, int yOffset)
        {
            // Scale the fingerprints and write to image
            for (int i = 0; i < fingerprintWidth /*128*/; i++)
            {
                for (int j = 0; j < fingerprintHeight /*32*/; j++)
                {
                    // if 10 or 01 element then its white
                    Color color = fingerprint[(2 * fingerprintHeight * i) + (2 * j)]
                                  || fingerprint[(2 * fingerprintHeight * i) + (2 * j) + 1]
                                      ? Color.White
                                      : Color.Black;
                    image.SetPixel(xOffset + i, yOffset + j, color);
                }
            }
        }

        private void SetBackground(Bitmap image, Color color)
        {
            for (int i = 0; i < image.Width; i++)
            {
                for (int j = 0; j < image.Height; j++)
                {
                    image.SetPixel(i, j, color);
                }
            }
        }
    }
}