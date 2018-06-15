using System.Collections.Generic;

namespace FindSimilarServices.Fingerprinting.Wavelets
{
	public class WaveletService : IWaveletService
	{
		private readonly IWaveletDecomposition waveletDecomposition;

		public WaveletService(IWaveletDecomposition waveletDecomposition)
		{
			this.waveletDecomposition = waveletDecomposition;
		}

		public void ApplyWaveletTransformInPlace(List<double[][]> logarithmizedSpectrum)
		{
			foreach (var image in logarithmizedSpectrum) {
				this.waveletDecomposition.DecomposeImageInPlace(image); /*Compute wavelets*/
			}
		}
	}
}