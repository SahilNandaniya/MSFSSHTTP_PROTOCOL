using MSFSSHTTP.Models;
using MSFSSHTTP.Services.SubRequestHandlers;

namespace MSFSSHTTP.Services
{
    public interface IMSFSSHTTPService
    {
        Task<(ResponseEnvelope Envelope, SubRequestContext Context)> CellStorageRequestNew(RequestEnvelope request, string filePath, string webRootPath);
    }
}
