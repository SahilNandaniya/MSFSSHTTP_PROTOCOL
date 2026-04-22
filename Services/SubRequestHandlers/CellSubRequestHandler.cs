using MSFSSHTTP.Models;
using MSFSSHTTP.Parsers;
using MSFSSHTTP.Utilities;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles Cell sub-requests (QueryChanges, QueryAccess, GetFileProps).
    /// Per MS-FSSHTTP Section 2.3.1.1 / 2.3.1.2.
    /// </summary>
    public class CellSubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => SubRequestAttributeType.Cell;

        public SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context)
        {
            var cellData = subRequest.SubRequestData;

            if (cellData != null && cellData.GetFileProps)
            {
                return BuildCellFilePropsResponse(subRequest, context);
            }

            return BuildCellResponse(subRequest);
        }

        private SubResponseElementGenericType BuildCellFilePropsResponse(
            SubRequestElementGenericType subReq, SubRequestContext context)
        {
            var fi = context.FileInfo;

            long lastModTicks = fi.Exists ? fi.LastWriteTimeUtc.ToFileTimeUtc() : 0;
            long createTicks = fi.Exists ? fi.CreationTimeUtc.ToFileTimeUtc() : 0;

            var etag = fi.Exists
                ? $"\"{{{GlobalConstant.StableDocumentId.ToString().ToUpper()}}},1,irm24A4B01E53DC46C79894148DC0051E64\""
                : "\"0\"";

            if (File.Exists(context.FilePath) && context.BinaryPayload == null)
            {
                var fileBytes = File.ReadAllBytes(context.FilePath);
                var storageGuid = Guid.NewGuid();
                ulong requestID = TryGetQueryChangesRequestId(subReq);
                context.BinaryPayload = FSSHTTPBResponseBuilder.BuildQueryChangesResponse(fileBytes, storageGuid, requestID);
            }

            return new SubResponseElementGenericType
            {
                SubRequestToken = subReq.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType
                {
                    Etag = etag,
                    CreateTime = createTicks.ToString(),
                    LastModifiedTime = lastModTicks.ToString(),
                    ModifiedBy = "Sahil Nandaniya",
                    HaveOnlyDemotionChanges = "False",
                    IsHybridCobalt = "True",
                }
            };
        }

        private static SubResponseElementGenericType BuildCellResponse(SubRequestElementGenericType subReq)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subReq.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType()
            };
        }

        /// <summary>
        /// Reads the Cell sub-request FSSHTTPB package (base64 in Text) and returns the RequestID
        /// of the QueryChanges sub-request (0x02). Word matches SubResponse RequestID to the request;
        /// a mismatch causes the client to reject the binary payload.
        /// </summary>
        private static ulong TryGetQueryChangesRequestId(SubRequestElementGenericType subReq)
        {
            try
            {
                var textArr = subReq.SubRequestData?.Text;
                if (textArr == null || textArr.Length == 0 || string.IsNullOrEmpty(textArr[0]))
                    return 1;

                var bytes = Convert.FromBase64String(textArr[0]);
                var fssReq = new FsshttpbRequest();
                fssReq.Parse(new MemoryStream(bytes));
                if (fssReq.SubRequest == null)
                    return 1;

                foreach (var sreq in fssReq.SubRequest)
                {
                    if (sreq.RequestType.GetUint(sreq.RequestType) == 0x02)
                        return sreq.RequestID.GetUint(sreq.RequestID);
                }
            }
            catch
            {
                // Ignore parse errors; use default RequestID.
            }

            return 1;
        }
    }
}
