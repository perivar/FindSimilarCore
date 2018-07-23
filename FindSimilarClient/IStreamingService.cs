using System.IO;
using System.Threading.Tasks;

namespace FindSimilarClient
{
    public interface IStreamingService
    {
         Task<Stream> GetByName(string name);
    }
}