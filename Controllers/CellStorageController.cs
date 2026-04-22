using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSFSSHTTP.Models;
using MSFSSHTTP.Services;
using System.Xml.Serialization;

namespace MSFSSHTTP.Controllers
{
    /// <summary>
    /// Handles MS-FSSHTTP CellStorage SOAP protocol requests.
    /// Route: /_vti_bin/cellstorage.svc/CellStorageService
    ///
    /// Incoming requests like /FSSHTTP/GetDoc/Doc.docx/_vti_bin/cellstorage.svc/CellStorageService
    /// are rewritten by VtiBinRoutingMiddleware to /_vti_bin/cellstorage.svc/CellStorageService
    /// before reaching routing. The original prefix is available via HttpContext.Items["VtiBinPrefix"].
    ///
    /// This is the core MS-FSSHTTP endpoint that Office clients POST to for co-authoring,
    /// cell storage queries, schema locks, and related operations.
    /// </summary>
    [Route("_vti_bin/cellstorage.svc/CellStorageService")]
    public class CellStorageController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public CellStorageController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ProcessCellStorageRequest()
        {
            string fileName = "BlankDocument.docx";  //ATTENTION: Hardcoded for testing purposes
            var filePath = Path.Combine(_env.WebRootPath, "files", fileName);

            var requestId = Guid.NewGuid().ToString();
            var uuid = Guid.NewGuid().ToString();
            var boundary = $"uuid:{uuid}+id=1";
            string contentId = DateTime.UtcNow.Ticks.ToString();

            SetResponseHeaders(requestId, boundary);

            // Parse SOAP envelope from request body
            var reqEnvelope = await ParseRequestEnvelope();
            if (reqEnvelope == null)
                return BadRequest("SOAP Envelope not found or invalid in request body.");

            try
            {
                // Process sub-requests with dependency graph resolution
                var service = HttpContext.RequestServices.GetRequiredService<IMSFSSHTTPService>();
                var (resp, context) = await service.CellStorageRequestNew(reqEnvelope, filePath, _env.WebRootPath);

                // Build MTOM multipart/related response
                var responseBytes = MtomResponseBuilder.Build(
                    reqEnvelope, resp, context,
                    boundary, contentId, requestId);

                return File(responseBytes, Response.ContentType);
            }
            catch (Exception ex)
            {
                return BadRequest($"Invalid XML: {ex.Message}");
            }
        }

        private void SetResponseHeaders(string requestId, string boundary)
        {
            Response.ContentType = $"multipart/related; boundary=\"{boundary}\"; type=\"application/xop+xml\"; start=\"<http://tempuri.org/0>\"; start-info=\"text/xml\"";
            Response.Headers["MIME-Version"] = "1.0";
            Response.Headers["Cache-Control"] = "private, max-age=0";
            Response.Headers["Accept-Ranges"] = "bytes";
            Response.Headers["DAV"] = "1,2";
            Response.Headers["Allow"] = "GET, POST, OPTIONS, HEAD, PUT, PROPFIND, LOCK, UNLOCK";
            Response.Headers["SPRequestGuid"] = requestId;
            Response.Headers["request-id"] = requestId;
            Response.Headers["X-MSDAVEXT"] = "1";
            Response.Headers["X-MSFSSHTTP"] = "1.6";
            Response.Headers["X-MS-InvokeApp"] = "1; RequireReadOnly";
            Response.Headers["X-DataBoundary"] = "NONE";
            Response.Headers["MicrosoftSharePointTeamServices"] = "16.0.0.27125";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Cookies.Append("SPA_RT", ";", new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddHours(8),
                Secure = true,
                SameSite = SameSiteMode.None
            });
        }

        private async Task<RequestEnvelope> ParseRequestEnvelope()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            int start = body.IndexOf("<s:Envelope", StringComparison.OrdinalIgnoreCase);
            int end = body.IndexOf("</s:Envelope>", StringComparison.OrdinalIgnoreCase);

            if (start < 0 || end <= start)
                return null;

            string envelopeXml = body.Substring(start, end - start + "</s:Envelope>".Length);

            var serializer = new XmlSerializer(typeof(RequestEnvelope));
            using StringReader xmlReader = new StringReader(envelopeXml);

            var reqObj = serializer.Deserialize(xmlReader);
            return reqObj as RequestEnvelope;
        }
    }
}
