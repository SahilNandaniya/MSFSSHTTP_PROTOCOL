using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles EditorsTable sub-requests.
    /// Per MS-FSSHTTP Section 2.3.1.24 / 3.1.4.8.
    /// </summary>
    public class EditorsTableSubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => SubRequestAttributeType.EditorsTable;

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
