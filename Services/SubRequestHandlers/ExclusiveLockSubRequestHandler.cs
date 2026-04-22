using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles ExclusiveLock sub-requests.
    /// Per MS-FSSHTTP Section 2.3.1.10 / 3.1.4.5.
    /// </summary>
    public class ExclusiveLockSubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => SubRequestAttributeType.ExclusiveLock;

        public SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequest.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType
                {
                    CoauthStatus = CoauthStatusType.Alone,
                    CoauthStatusSpecified = true,
                    TransitionID = Guid.NewGuid().ToString()
                }
            };
        }
    }
}
