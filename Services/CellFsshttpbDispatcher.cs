using MSFSSHTTP.Parsers;

namespace MSFSSHTTP.Services
{
    /// <summary>
    /// Parses an incoming FSSHTTPB Cell request and builds a single aggregated FSSHTTPB response
    /// (ack-only: no data element merge).
    /// </summary>
    public static class CellFsshttpbDispatcher
    {
        /// <exception cref="InvalidOperationException">Empty or invalid FSSHTTPB request.</exception>
        /// <exception cref="NotSupportedException">Unknown sub-request type.</exception>
        public static byte[] BuildAggregatedResponse(byte[] requestBytes)
        {
            if (requestBytes == null || requestBytes.Length == 0)
            {
                throw new InvalidOperationException("FSSHTTPB request bytes are empty.");
            }

            var req = new FsshttpbRequest();
            req.Parse(new MemoryStream(requestBytes));

            if (req.SubRequest == null || req.SubRequest.Length == 0)
            {
                throw new InvalidOperationException("FSSHTTPB request has no sub-requests.");
            }

            var list = new List<FsshttpbSubResponse>(req.SubRequest.Length);
            var uintReader = new bit32StreamObjectHeaderStart();
            foreach (var sr in req.SubRequest)
            {
                var type = uintReader.GetUint(sr.RequestType);
                var rid = uintReader.GetUint(sr.RequestID);

                switch (type)
                {
                    case 1:
                        list.Add(FSSHTTPBResponseBuilder.CreateQueryAccessSubResponse(rid));
                        break;
                    case 2:
                        list.Add(FSSHTTPBResponseBuilder.CreateEmptyQueryChangesSubResponse(rid));
                        break;
                    case 5:
                        if (sr.SubRequestData is not PutChangesRequest putReq)
                        {
                            throw new InvalidOperationException("PutChanges sub-request missing parsed body.");
                        }

                        list.Add(FSSHTTPBResponseBuilder.CreatePutChangesSubResponse(rid, putReq));
                        break;
                    case 11: // 0x0B AllocateExtendedGUIDRange
                        list.Add(FSSHTTPBResponseBuilder.CreateAllocateExtendedGuidRangeSubResponse(
                            rid,
                            sr.SubRequestData as AllocateExtendedGUIDRangeRequest));
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported FSSHTTPB sub-request type {type}.");
                }
            }

            return FSSHTTPBResponseBuilder.BuildFsshttpbResponseBytes(list.ToArray());
        }
    }
}
