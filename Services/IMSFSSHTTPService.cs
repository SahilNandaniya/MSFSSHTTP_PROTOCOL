using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services
{
    public interface IMSFSSHTTPService
    {
        Task<ResponseEnvelope> CellStorageRequestNew(RequestEnvelope request, string filePath);
    }
}
