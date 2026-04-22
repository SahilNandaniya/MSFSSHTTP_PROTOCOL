using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles GetVersions sub-requests.
    /// Per MS-FSSHTTP Section 2.3.1.31 / 3.1.4.10.
    /// </summary>
    public class GetVersionsSubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => SubRequestAttributeType.GetVersions;

        public SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequest.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "2147500037",
                GetVersionsResponse = new GetVersionsResponseType()
                {
                    GetVersionsResult = new GetVersionsResult()
                    {
                        results = new Models.Results
                        {
                            list = new ResultsList() { id = "00000000-0000-0000-0000-000000000000" },
                            versioning = new ResultsVersioning() { enabled = 0 },
                            settings = new ResultsSettings() { url = "" },
                        }
                    }
                }
            };
        }
    }
}
