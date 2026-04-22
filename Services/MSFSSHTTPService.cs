using MSFSSHTTP.Models;
using MSFSSHTTP.Services.SubRequestHandlers;
using MSFSSHTTP.Utilities;

namespace MSFSSHTTP.Services
{
    /// <summary>
    /// Orchestrates MS-FSSHTTP cell storage request processing.
    ///
    /// For each Request in the RequestCollection:
    /// 1. Topologically sorts sub-requests by dependency graph
    /// 2. Evaluates dependency conditions per MS-FSSHTTP Section 3.1.4.1
    /// 3. Dispatches to the appropriate ISubRequestHandler
    /// 4. Builds the ResponseEnvelope with correctly ordered SubResponses
    /// </summary>
    public class MSFSSHTTPService : IMSFSSHTTPService
    {
        private readonly Dictionary<SubRequestAttributeType, ISubRequestHandler> _handlers;
        private readonly DefaultSubRequestHandler _defaultHandler;

        public MSFSSHTTPService(IEnumerable<ISubRequestHandler> handlers)
        {
            _defaultHandler = new DefaultSubRequestHandler();
            _handlers = new Dictionary<SubRequestAttributeType, ISubRequestHandler>();

            foreach (var handler in handlers)
            {
                _handlers[handler.HandledType] = handler;
            }
        }

        public Task<(ResponseEnvelope Envelope, SubRequestContext Context)> CellStorageRequestNew(
            RequestEnvelope request, string filePath, string webRootPath)
        {
            var requestCollection = request.Body?.RequestCollection;
            if (requestCollection?.Request == null || requestCollection.Request.Length == 0)
            {
                return Task.FromResult((BuildErrorResponse("No requests in collection."), (SubRequestContext)null));
            }

            var context = new SubRequestContext(filePath, webRootPath);
            var responses = new List<Response>();

            foreach (var req in requestCollection.Request)
            {
                var subResponses = ProcessSubRequestsWithDependencies(req.SubRequest, context);

                responses.Add(new Response
                {
                    Url = req.Url,
                    UrlIsEncoded = "False",
                    RequestToken = req.RequestToken,
                    HealthScore = "2",
                    RequestedClientImpact = 3,
                    IntervalOverride = "0",
                    ExpectedAccessRead = true,
                    ExpectedAccessWrite = true,
                    ResourceID = GlobalConstant.StableDocumentId.ToString("N"),
                    ErrorCode = GenericErrorCodeTypes.Success,
                    ErrorCodeSpecified = false,
                    SubResponse = subResponses.ToArray(),
                    TenantId = "e777e544-630e-475c-a07a-cd4143ce5140"
                });
            }

            var envelope = new ResponseEnvelope
            {
                Body = new ResponseEnvelopeBody
                {
                    ResponseVersion = new ResponseVersion
                    {
                        Version = 2,
                        MinorVersion = 3
                    },
                    ResponseCollection = new ResponseCollection
                    {
                        WebUrl = "https://pnq1-lhp-n91356/FSSHTTP/GetDoc/",
                        WebUrlIsEncoded = "False",
                        Response = responses.ToArray()
                    }
                }
            };

            return Task.FromResult((envelope, context));
        }

        /// <summary>
        /// Processes sub-requests following the dependency graph order.
        ///
        /// 1. Topologically sorts sub-requests so parents execute before dependents
        /// 2. Dispatches each sub-request to its handler in dependency order
        /// 3. Returns sub-responses in the ORIGINAL sub-request order (not topological order),
        ///    as the protocol requires SubResponse ordering to match SubRequest ordering.
        ///
        /// Note: Dependency evaluation (skipping sub-requests whose dependency condition
        /// is not met) is structurally supported via SubRequestDependencyResolver but is
        /// not yet enforced - all sub-requests are currently executed to preserve existing
        /// behavior. Enable enforcement when ready by uncommenting the dependency check.
        /// </summary>
        private List<SubResponseElementGenericType> ProcessSubRequestsWithDependencies(
            SubRequestElementGenericType[] subRequests, SubRequestContext context)
        {
            if (subRequests == null || subRequests.Length == 0)
                return new List<SubResponseElementGenericType>();

            // Sort by dependency graph so parents are processed before dependents
            var sorted = SubRequestDependencyResolver.TopologicalSort(subRequests);

            // Track results by SubRequestToken
            var resultsByToken = new Dictionary<string, SubResponseElementGenericType>();

            foreach (var subReq in sorted)
            {
                // Execute the handler
                var subResponse = DispatchToHandler(subReq, context);
                resultsByToken[subReq.SubRequestToken] = subResponse;
            }

            // Return responses in original SubRequest order
            var orderedResponses = new List<SubResponseElementGenericType>();
            foreach (var subReq in subRequests)
            {
                if (resultsByToken.TryGetValue(subReq.SubRequestToken, out var response))
                    orderedResponses.Add(response);
            }

            return orderedResponses;
        }

        private SubResponseElementGenericType DispatchToHandler(
            SubRequestElementGenericType subReq, SubRequestContext context)
        {
            if (_handlers.TryGetValue(subReq.Type, out var handler))
            {
                return handler.Handle(subReq, context);
            }

            return _defaultHandler.Handle(subReq, context);
        }

        private static ResponseEnvelope BuildErrorResponse(string errorMessage)
        {
            return new ResponseEnvelope
            {
                Body = new ResponseEnvelopeBody
                {
                    ResponseVersion = new ResponseVersion
                    {
                        Version = 2,
                        MinorVersion = 2,
                        ErrorCode = GenericErrorCodeTypes.InvalidSubRequest,
                        ErrorCodeSpecified = true,
                        ErrorMessage = errorMessage
                    }
                }
            };
        }
    }
}
