// Sound Fingerprinting framework
// git://github.com/AddictedCS/FindSimilarServices.Fingerprinting.git
// Code license: CPOL v.1.02
// ciumac.sergiu@gmail.com
namespace FindSimilarServices.Fingerprinting.Hashing
{
    /// <summary>
    ///   Permutations storage
    /// </summary>
    public interface IPermutations
    {
        /// <summary>
        ///   Get Min Hash random permutations
        /// </summary>
        /// <returns>Permutation indexes</returns>
        int[][] GetPermutations();
    }
}