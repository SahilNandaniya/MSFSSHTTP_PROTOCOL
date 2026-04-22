using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles ServerTime sub-requests.
    /// Per MS-FSSHTTP Section 2.3.1.17 / 3.1.4.6.
    /// </summary>
    public class ServerTimeSubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => SubRequestAttributeType.ServerTime;

        public SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context)
        {
            var serverTimeTicks = DateTimeOffset.UtcNow.Ticks;
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequest.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType
                {
                    ServerTime = serverTimeTicks.ToString()
                }
            };
        }
    }
}
