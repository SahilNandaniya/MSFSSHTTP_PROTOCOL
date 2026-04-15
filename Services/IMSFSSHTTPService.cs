using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services
{
    public interface IMSFSSHTTPService
    {
        /// <param name="responseWebUrl">SOAP ResponseCollection WebUrl (scheme + host + document library path).</param>
        /// <param name="cellFsshttpbRequestBytesBySubRequestToken">Decoded FSSHTTPB Cell request payload per SOAP SubRequestToken (from base64 or MTOM).</param>
        Task<(ResponseEnvelope Envelope, byte[]? BinaryPayload, IReadOnlyDictionary<string, byte[]> AggregatedCellFsshttpbBySubRequestToken)> CellStorageRequestNew(
            RequestEnvelope request,
            string filePath,
            string responseWebUrl,
            IReadOnlyDictionary<string, byte[]>? cellFsshttpbRequestBytesBySubRequestToken);

        //Task<ResponseEnvelope> SharedAccessRequestNew(RequestEnvelope request);
    }
}
