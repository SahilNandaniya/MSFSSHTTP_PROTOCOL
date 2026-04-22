using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles WhoAmI sub-requests.
    /// Per MS-FSSHTTP Section 2.3.1.20 / 3.1.4.7.
    /// </summary>
    public class WhoAmISubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => SubRequestAttributeType.WhoAmI;

        public SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequest.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType
                {
                    UserName = "Nandaniya Sahil Bharatbhai",
                    UserEmailAddress = "sahil.bharatbhai@anaplan.com",
                    UserIsAnonymous = false,
                    UserIsAnonymousSpecified = true,
                    UserLogin = "sahil.bharatbhai@anaplan.com",
                    UserSIPAddress = "sahil.bharatbhai@anaplan.com"
                }
            };
        }
    }
}
