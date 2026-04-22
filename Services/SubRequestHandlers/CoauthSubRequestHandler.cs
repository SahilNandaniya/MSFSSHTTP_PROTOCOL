using MSFSSHTTP.Models;
using MSFSSHTTP.Utilities;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles Coauth sub-requests.
    /// Per MS-FSSHTTP Section 2.3.1.5 / 3.1.4.3.
    /// </summary>
    public class CoauthSubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => SubRequestAttributeType.Coauth;

        public SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context)
        {
            // Store SchemaLockId in context for downstream handlers (e.g., GetDocMetaInfo)
            context.SchemaLockId = subRequest.SubRequestData?.SchemaLockID;

            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequest.SubRequestToken,
                HResult = "0",
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                SubResponseData = new SubResponseDataGenericType
                {
                    CoauthStatus = CoauthStatusType.Alone,
                    LockType = LockTypes.SchemaLock.ToString(),
                    LockTypeSpecified = true,
                    CoauthStatusSpecified = true,
                    TransitionID = GlobalConstant.StableDocumentId.ToString()
                }
            };
        }
    }
}
