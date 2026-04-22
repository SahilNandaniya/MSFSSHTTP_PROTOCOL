using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles SchemaLock sub-requests.
    /// Per MS-FSSHTTP Section 2.3.1.13 / 3.1.4.4.
    /// </summary>
    public class SchemaLockSubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => SubRequestAttributeType.SchemaLock;

        public SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequest.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.DependentOnlyOnNotSupportedRequestGetSupported.ToString(),
                HResult = "2147500037",
                SubResponseData = new SubResponseDataGenericType()
            };
        }
    }
}
