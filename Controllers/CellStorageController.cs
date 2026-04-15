using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSFSSHTTP.Models;
using MSFSSHTTP.Services;
using MSFSSHTTP.Utilities;
using System.Text;
using System.Xml;
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

            // Set multipart/related content type
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

            Request.EnableBuffering();
            byte[] bodyBytes;
            await using (var msBody = new MemoryStream())
            {
                await Request.Body.CopyToAsync(msBody);
                bodyBytes = msBody.ToArray();
            }

            var body = Encoding.UTF8.GetString(bodyBytes);

            int start = body.IndexOf("<s:Envelope", StringComparison.OrdinalIgnoreCase);
            int end = body.IndexOf("</s:Envelope>", StringComparison.OrdinalIgnoreCase);

            if (start < 0 || end <= start)
            {
                return BadRequest("SOAP Envelope not found in request body.");
            }

            string envelopeXml = body.Substring(start, end - start + "</s:Envelope>".Length);

            IReadOnlyDictionary<string, byte[]>? mtomParts = null;
            if (MtomMultipartParser.TryParseMultipart(Request.ContentType, bodyBytes, out var mtomDict))
            {
                mtomParts = mtomDict;
            }

            var cellFsshttpbByToken = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            XmlSerializer serializer = new XmlSerializer(typeof(RequestEnvelope));
            using StringReader xmlReader = new StringReader(envelopeXml);

            var reqObj = serializer.Deserialize(xmlReader);
            if (reqObj is not RequestEnvelope req)
                return BadRequest("Deserialized object is not a valid RequestEnvelope.");

            foreach (var r in req.Body?.RequestCollection?.Request ?? Array.Empty<Request>())
            {
                foreach (var sr in r.SubRequest ?? Array.Empty<SubRequestElementGenericType>())
                {
                    if (sr.Type != SubRequestAttributeType.Cell)
                    {
                        continue;
                    }

                    if (sr.SubRequestData?.GetFileProps == true)
                    {
                        continue;
                    }

                    if (CellRequestBinaryResolver.TryGetFsshttpbRequestBytes(sr, mtomParts, out var fssBytes)
                        && fssBytes.Length > 0)
                    {
                        cellFsshttpbByToken[sr.SubRequestToken] = fssBytes;
                    }
                }
            }

            var prefix = HttpContext.Items["VtiBinPrefix"] as string ?? string.Empty;
            var pathForWebUrl = prefix;
            if (!string.IsNullOrEmpty(pathForWebUrl))
            {
                var lastSlash = pathForWebUrl.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    pathForWebUrl = pathForWebUrl[..lastSlash];
                }
            }

            if (string.IsNullOrEmpty(pathForWebUrl) || !pathForWebUrl.StartsWith('/'))
            {
                pathForWebUrl = "/FSSHTTP/GetDoc";
            }

            var responseWebUrl = $"{Request.Scheme}://{Request.Host}{pathForWebUrl.TrimEnd('/')}/";

            try
            {
                var service = HttpContext.RequestServices.GetRequiredService<IMSFSSHTTPService>();
                var (resp, binaryPayload, aggregatedCell) = await service.CellStorageRequestNew(
                    req,
                    filePath,
                    responseWebUrl,
                    cellFsshttpbByToken);

                // Get all Cell SubRequests and classify by partition
                var allCellSubRequests = req.Body?.RequestCollection?.Request?
                    .SelectMany(r => r.SubRequest ?? Array.Empty<SubRequestElementGenericType>())
                    .Where(sr => sr.Type == SubRequestAttributeType.Cell && !string.IsNullOrEmpty(sr.DependsOn))
                    .ToList() ?? new List<SubRequestElementGenericType>();

                var fileContentsCells = allCellSubRequests
                    .Where(sr => string.IsNullOrWhiteSpace(sr.SubRequestData?.PartitionID))
                    .ToList();

                var editorTableCells = allCellSubRequests
                    .Where(sr => string.Equals(sr.SubRequestData?.PartitionID,
                        "7808F4DD-2385-49D6-B7CE-37ACA5E43602",
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var metadataCells = allCellSubRequests
                    .Where(sr => string.Equals(sr.SubRequestData?.PartitionID,
                        "383ADC0B-E66E-4438-95E6-E39EF9720122",
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // QueryAccess: Cell SubRequests with no DependsOn (independent, RequestType=1)
                var queryAccessCells = req.Body?.RequestCollection?.Request?
                    .SelectMany(r => r.SubRequest ?? Array.Empty<SubRequestElementGenericType>())
                    .Where(sr => sr.Type == SubRequestAttributeType.Cell
                        && string.IsNullOrEmpty(sr.DependsOn)
                        && sr.SubRequestData?.GetFileProps != true)
                    .ToList() ?? new List<SubRequestElementGenericType>();

                // Build binaries for each partition
                byte[]? fileContentsBytes = Array.Empty<byte>();
                byte[]? editorTableBytes = Array.Empty<byte>();
                byte[]? metadataBytes = Array.Empty<byte>();
                byte[]? queryAccessBytes = Array.Empty<byte>();

                if (fileContentsCells.Count > 0)
                {
                    fileContentsBytes = binaryPayload ?? Array.Empty<byte>();
                }

                if (editorTableCells.Count > 0)
                {
                    editorTableBytes = FSSHTTPBResponseBuilder.BuildMinimalPartitionResponse(_env.WebRootPath);
                }

                if (metadataCells.Count > 0)
                {
                    metadataBytes = FSSHTTPBResponseBuilder.BuildEmptyStorageIndexResponse();
                }

                if (queryAccessCells.Count > 0)
                {
                    queryAccessBytes = FSSHTTPBResponseBuilder.BuildQueryAccessResponse();
                }

                // Update SubResponseData with correct values for each partition
                if (resp.Body?.ResponseCollection?.Response != null)
                {
                    var fileInfo = new FileInfo(filePath);

                    foreach (var response in resp.Body.ResponseCollection.Response)
                    {
                        if (response?.SubResponse == null)
                        {
                            continue;
                        }

                        // MIME part indices must match the fixed order in which parts are written:
                        // Part 0 = SOAP XML envelope
                        // Part 1 = editors table (if present)
                        // Part N = file contents (next index after editors table)
                        int editorsTableMimeIndex = 1;
                        int fileContentsMimeIndex = (editorTableBytes?.Length ?? 0) > 0 ? 2 : 1;

                        foreach (var subResponse in response.SubResponse)
                        {
                            if (subResponse?.SubResponseData == null)
                            {
                                continue;
                            }

                            if (aggregatedCell.TryGetValue(subResponse.SubRequestToken, out var aggBytes)
                                && aggBytes != null
                                && aggBytes.Length > 0)
                            {
                                subResponse.SubResponseData.Text = new[] { Convert.ToBase64String(aggBytes) };
                                subResponse.ServerCorrelationId = requestId;
                                continue;
                            }

                            var cellSubReq = allCellSubRequests
                                .FirstOrDefault(sr => sr.SubRequestToken == subResponse.SubRequestToken);

                            var queryAccessSubReq = queryAccessCells
                                .FirstOrDefault(sr => sr.SubRequestToken == subResponse.SubRequestToken);

                            if (cellSubReq == null && queryAccessSubReq == null)
                            {
                                continue;
                            }

                            if (queryAccessSubReq != null && queryAccessBytes!.Length > 0)
                            {
                                // QueryAccess (RequestType=1): base64-encoded FSSHTTPB binary as inline text
                                subResponse.SubResponseData.Text = new[] { Convert.ToBase64String(queryAccessBytes!) };
                            }
                            else if (cellSubReq != null)
                            {
                                bool isFileContents = string.IsNullOrWhiteSpace(cellSubReq.SubRequestData?.PartitionID);
                                bool isMetadata = string.Equals(cellSubReq.SubRequestData?.PartitionID,
                                    "383ADC0B-E66E-4438-95E6-E39EF9720122",
                                    StringComparison.OrdinalIgnoreCase);
                                bool isEditorsTable = string.Equals(cellSubReq.SubRequestData?.PartitionID,
                                    "7808F4DD-2385-49D6-B7CE-37ACA5E43602",
                                    StringComparison.OrdinalIgnoreCase);

                                if (isFileContents && fileContentsBytes != null)
                                {
                                    // File contents: MTOM MIME part with file metadata
                                    subResponse.SubResponseData.Include = new Include
                                    {
                                        href = $"cid:http://tempuri.org/{fileContentsMimeIndex}/{contentId}"
                                    };
                                    subResponse.ServerCorrelationId = requestId;
                                }
                                else if (isEditorsTable && editorTableBytes != null)
                                {
                                    // Editors table: MTOM MIME part without metadata
                                    subResponse.SubResponseData.Include = new Include
                                    {
                                        href = $"cid:http://tempuri.org/{editorsTableMimeIndex}/{contentId}"
                                    };
                                }
                                else if (isMetadata && metadataBytes != null && metadataBytes.Length > 0)
                                {
                                    // Metadata: no MTOM MIME part, no Include element
                                    // Send FSSHTTPB binary inline as base64-encoded text content
                                    subResponse.SubResponseData.Text = new[] { Convert.ToBase64String(metadataBytes!) };
                                    subResponse.ServerCorrelationId = requestId;
                                }
                            }
                        }
                    }
                }

                // Serialize protocol XML (no XML declaration, correct namespaces)
                var xmlWriterSettings = new XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Encoding = Encoding.UTF8,
                    Indent = false
                };
                var ns = new XmlSerializerNamespaces();
                ns.Add("s", "http://schemas.xmlsoap.org/soap/envelope/");
                ns.Add("", ""); // Default for other elements

                string xmlPart;
                using (var sw = new StringWriter())
                using (var xw = XmlWriter.Create(sw, xmlWriterSettings))
                {
                    var respSerializer = new XmlSerializer(typeof(ResponseEnvelope));
                    respSerializer.Serialize(xw, resp, ns);
                    xmlPart = sw.ToString();
                }

                // Build multipart response
                var sb = new StringBuilder();
                sb.Append($"--{boundary}\r\n");
                sb.Append("Content-ID: <http://tempuri.org/0>\r\n");
                sb.Append("Content-Transfer-Encoding: 8bit\r\n");
                sb.Append("Content-Type: application/xop+xml; charset=utf-8; type=\"text/xml\"\r\n");
                sb.Append("\r\n");
                sb.Append(xmlPart);

                // Convert XML part to bytes
                var xmlBytes = Encoding.UTF8.GetBytes(sb.ToString());

                // Combine all parts
                var responseBytes = new List<byte>();
                responseBytes.AddRange(xmlBytes);

                // Add editors table MIME part (if present)
                if (editorTableBytes?.Length > 0)
                {
                    var editorsAttachmentHeaders = new StringBuilder();
                    editorsAttachmentHeaders.Append($"\r\n--{boundary}\r\n");
                    editorsAttachmentHeaders.Append($"Content-ID: <http://tempuri.org/1/{contentId}>\r\n");
                    editorsAttachmentHeaders.Append("Content-Transfer-Encoding: binary\r\n");
                    editorsAttachmentHeaders.Append("Content-Type: application/octet-stream\r\n");
                    editorsAttachmentHeaders.Append("\r\n");

                    responseBytes.AddRange(Encoding.UTF8.GetBytes(editorsAttachmentHeaders.ToString()));
                    responseBytes.AddRange(editorTableBytes!);
                }

                // Add file contents MIME part (if present)
                if (fileContentsBytes?.Length > 0)
                {
                    var fileAttachmentHeaders = new StringBuilder();
                    int fileContentMimeIndex = (editorTableBytes?.Length ?? 0) > 0 ? 2 : 1;

                    fileAttachmentHeaders.Append($"\r\n--{boundary}\r\n");
                    fileAttachmentHeaders.Append($"Content-ID: <http://tempuri.org/{fileContentMimeIndex}/{contentId}>\r\n");
                    fileAttachmentHeaders.Append("Content-Transfer-Encoding: binary\r\n");
                    fileAttachmentHeaders.Append("Content-Type: application/octet-stream\r\n");
                    fileAttachmentHeaders.Append("\r\n");

                    responseBytes.AddRange(Encoding.UTF8.GetBytes(fileAttachmentHeaders.ToString()));
                    responseBytes.AddRange(fileContentsBytes!);
                }

                // Add closing boundary
                var endBoundary = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
                responseBytes.AddRange(endBoundary);

                // Return as FileContentResult
                return File(responseBytes.ToArray(), Response.ContentType);
            }
            catch (Exception ex)
            {
                return BadRequest($"Invalid XML: {ex.Message}");
            }
        }
    }
}
