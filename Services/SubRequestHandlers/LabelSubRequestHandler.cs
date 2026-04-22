using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles Label sub-requests.
    /// Per MS-FSSHTTP Section 2.3.1.51.
    /// </summary>
    public class LabelSubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => SubRequestAttributeType.Label;

        public SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequest.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.FeatureDisabledOnServerTenantNotSupported.ToString(),
                HResult = "2147500037",
                SubResponseData = new SubResponseDataGenericType()
            };
        }
    }
}
