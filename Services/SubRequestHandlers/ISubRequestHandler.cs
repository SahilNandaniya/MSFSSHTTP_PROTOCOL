using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles a specific type of MS-FSSHTTP sub-request and produces the corresponding sub-response.
    /// Each implementation handles exactly one SubRequestAttributeType.
    /// </summary>
    public interface ISubRequestHandler
    {
        SubRequestAttributeType HandledType { get; }
        SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context);
    }
}
