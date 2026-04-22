using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Fallback handler for sub-request types not explicitly handled.
    /// Returns a generic success response with empty SubResponseData.
    /// </summary>
    public class DefaultSubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => default;

        public SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequest.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType()
            };
        }
    }
}
