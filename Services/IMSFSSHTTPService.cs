using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services
{
    public interface IMSFSSHTTPService
    {
        Task<(ResponseEnvelope Envelope, byte[] BinaryPayload)> CellStorageRequestNew(RequestEnvelope request, string filePath);

        //Task<ResponseEnvelope> SharedAccessRequestNew(RequestEnvelope request);
    }
}
