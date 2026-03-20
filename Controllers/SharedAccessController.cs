using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace MSFSSHTTP.Controllers
{
    /// <summary>
    /// Handles the SharePoint SharedAccess web service operations.
    /// Route: /_vti_bin/sharedaccess.asmx
    ///
    /// Incoming requests like /FSSHTTP/GetDoc/Doc.docx/_vti_bin/sharedaccess.asmx
    /// are rewritten by VtiBinRoutingMiddleware to /_vti_bin/sharedaccess.asmx
    /// before reaching routing. The original prefix is available via HttpContext.Items["VtiBinPrefix"].
    ///
    /// Supports:
    ///   - IsOnlyClient   → returns IsOnlyClientResult = true
    ///   - CheckUserAccess → returns full-permission UserAccess element
    /// </summary>
    [Route("_vti_bin/sharedaccess.asmx")]
    public class SharedAccessController : Controller
    {
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ProcessSharedAccessRequest()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var soapRequest = await reader.ReadToEndAsync();

            // Detect the SOAP operation from the SOAPAction header first, then fall back to body inspection
            Request.Headers.TryGetValue("SOAPAction", out var soapActionValues);
            var soapAction = soapActionValues.ToString().Trim('"');

            // Apply SharePoint-compatible response headers (mirroring real SP behaviour)
            var requestId = Guid.NewGuid().ToString("D");
            Response.Headers["Cache-Control"] = "private, max-age=0";
            Response.Headers["SPRequestGuid"] = requestId;
            Response.Headers["request-id"] = requestId;
            Response.Headers["SPLogId"] = requestId;
            Response.Headers["X-SharePointHealthScore"] = "0";
            Response.Headers["MicrosoftSharePointTeamServices"] = "16.0.0.27104";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["X-MS-InvokeApp"] = "1; RequireReadOnly";
            Response.Headers["X-Powered-By"] = "ASP.NET";
            Response.Headers["IsOCDI"] = "0";
            Response.Headers["X-DataBoundary"] = "NONE";

            string responseXml;

            bool isIsOnlyClient = soapAction.Contains("IsOnlyClient", StringComparison.OrdinalIgnoreCase)
                                     || soapRequest.Contains("IsOnlyClient", StringComparison.OrdinalIgnoreCase);

            bool isCheckUserAccess = soapAction.Contains("CheckUserAccess", StringComparison.OrdinalIgnoreCase)
                                     || soapRequest.Contains("CheckUserAccess", StringComparison.OrdinalIgnoreCase);

            if (isIsOnlyClient)
            {
                responseXml = BuildIsOnlyClientResponse();
            }
            else if (isCheckUserAccess)
            {
                responseXml = BuildCheckUserAccessResponse();
            }
            else
            {
                // Unknown operation: return a well-formed SOAP fault
                responseXml = BuildSoapFault("soap:Server", $"Operation not supported: {soapAction}");
            }

            return Content(responseXml, "text/xml", Encoding.UTF8);
        }

        /// <summary>
        /// Builds the IsOnlyClient SOAP response.
        /// Matches the exact body returned by SharePoint Online for this operation.
        /// </summary>
        private static string BuildIsOnlyClientResponse() =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
                           "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
                           "xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
              "<soap:Body>" +
                "<IsOnlyClientResponse xmlns=\"http://schemas.microsoft.com/sharepoint/soap/\">" +
                  "<IsOnlyClientResult>true</IsOnlyClientResult>" +
                "</IsOnlyClientResponse>" +
              "</soap:Body>" +
            "</soap:Envelope>";

        /// <summary>
        /// Builds a CheckUserAccess SOAP response that grants full access permissions.
        /// Permission mask 0x7FFFFFFFFFFFFFFF = all SPBasePermissions bits set (MS-WSSO).
        /// </summary>
        private static string BuildCheckUserAccessResponse()
        {
            const string fullPermissionMask = "9223372036854775807"; // 0x7FFFFFFFFFFFFFFF

            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                   "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
                                  "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
                                  "xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
                     "<soap:Body>" +
                       "<CheckUserAccessResponse xmlns=\"http://schemas.microsoft.com/sharepoint/soap/\">" +
                         "<CheckUserAccessResult>" +
                           $"<UserAccess AccessDenied=\"false\" PermissionsMask=\"{fullPermissionMask}\" />" +
                         "</CheckUserAccessResult>" +
                       "</CheckUserAccessResponse>" +
                     "</soap:Body>" +
                   "</soap:Envelope>";
        }

        /// <summary>
        /// Builds a SOAP 1.1 fault response for unsupported or erroneous operations.
        /// </summary>
        private static string BuildSoapFault(string faultCode, string faultString) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
              "<soap:Body>" +
                "<soap:Fault>" +
                  $"<faultcode>{faultCode}</faultcode>" +
                  $"<faultstring>{System.Security.SecurityElement.Escape(faultString)}</faultstring>" +
                "</soap:Fault>" +
              "</soap:Body>" +
            "</soap:Envelope>";
    }
}
