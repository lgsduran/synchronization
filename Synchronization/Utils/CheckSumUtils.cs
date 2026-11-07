using System.Security.Cryptography;
namespace Synchronization.Utils
{
    /// <summary>
    /// Class <c>CheckSumUtils</c> utility to render hash for the input data.
    /// </summary>
    public class CheckSumUtils
    {
        /// <summary>
        /// Method <c>CheckSumUtils</c> that returns the SHA256 hash for the input data
        /// </summary>
        /// <returns>
        /// A string representing hash for the input data.
        /// </returns>
        public string SHA256CheckSum(string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(fileStream));
        }
    }
}
