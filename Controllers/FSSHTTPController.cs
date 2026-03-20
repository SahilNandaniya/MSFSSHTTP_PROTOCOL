using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSFSSHTTP.Models;
using MSFSSHTTP.Services;
using System.ServiceModel;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace MSFSSHTTP.Controllers
{
    [ServiceContract]
    public class FSSHTTPController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public FSSHTTPController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [AcceptVerbs(new[] { "GET", "POST", "HEAD", "LOCK", "OPTIONS", "PROPFIND", "PUT", "UNLOCK" })]
        [AllowAnonymous]
        public async Task<IActionResult> GetDoc(string? id)
        {
            var method = Request.Method;
            string fileName = "DemoFSSHTTPDocument.docx";  //ATTENTION: Hardcoded for testing purposes

            //string? fileName = id;
            //if (string.IsNullOrWhiteSpace(fileName))
            //    return BadRequest("Missing file name.");

            var filePath = Path.Combine(_env.WebRootPath, "files", fileName);

            if (method == "OPTIONS")
            {
                var requestId = Guid.NewGuid().ToString();
                Response.Headers["Cache-Control"] = "private, max-age=0";
                Response.Headers["X-Cache"] = "CONFIG_NOCACHE";
                Response.Headers["Accept-Ranges"] = "bytes";
                Response.Headers["DAV"] = "1,2";
                Response.Headers["Allow"] = "GET, POST, OPTIONS, HEAD, PUT, PROPFIND, LOCK, UNLOCK";
                Response.Headers["SPRequestGuid"] = requestId;
                Response.Headers["request-id"] = requestId;
                Response.Headers["X-MSDAVEXT"] = "1";
                Response.Headers["X-MSFSSHTTP"] = "1.6";
                Response.Headers["X-MS-InvokeApp"] = "1; RequireReadOnly";
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                Response.Cookies.Append("SPA_RT", ";", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddHours(8),
                    Secure = true,
                    SameSite = SameSiteMode.None
                });
                return Ok();
            }
            else if (method == "HEAD")
            {
                if (!System.IO.File.Exists(filePath))
                    return NotFound();

                var fi = new FileInfo(filePath);
                Response.Headers["Content-Length"] = fi.Length.ToString();
                Response.Headers["Accept-Ranges"] = "bytes";
                Response.Headers["Content-Type"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                Response.Headers["Last-Modified"] = fi.LastWriteTimeUtc.ToString("R");
                Response.Headers["ETag"] = $"\"{fi.LastWriteTimeUtc.Ticks:x}\"";
                return Ok();
            }
            else if (method == "GET")
            {
                if (!System.IO.File.Exists(filePath))
                    return NotFound($"File {fileName} not found.");

                var fi = new FileInfo(filePath);
                Response.Headers["Accept-Ranges"] = "bytes";
                Response.Headers["Last-Modified"] = fi.LastWriteTimeUtc.ToString("R");
                Response.Headers["ETag"] = $"\"{fi.LastWriteTimeUtc.Ticks:x}\"";

                var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                const string contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                return File(stream, contentType, fileName);
            }
            else if (method == "POST")
            {
                var requestId = Guid.NewGuid().ToString();
                var uuid = Guid.NewGuid().ToString();
                var boundary = $"uuid:{uuid}+id=1";
                string contentId = DateTime.UtcNow.Ticks.ToString();
                var xopContentId = $"http://tempuri.org/1/{contentId}";
                var xopHref = $"cid:{xopContentId}";

                // Set multipart/related content type
                Response.ContentType = $"multipart/related; boundary=\"{boundary}\"; type=\"application/xop+xml\"; start=\"<rootpart@tempuri.org>\"; start-info=\"text/xml\"";
                Response.Headers["Cache-Control"] = "private, max-age=0";
                Response.Headers["Accept-Ranges"] = "bytes";
                Response.Headers["DAV"] = "1,2";
                Response.Headers["Allow"] = "GET, POST, OPTIONS, HEAD, PUT, PROPFIND, LOCK, UNLOCK";
                Response.Headers["SPRequestGuid"] = requestId;
                Response.Headers["request-id"] = requestId;
                Response.Headers["X-MSDAVEXT"] = "1";
                Response.Headers["X-MSFSSHTTP"] = "1.6";
                Response.Headers["X-MS-InvokeApp"] = "1; RequireReadOnly";
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                Response.Cookies.Append("SPA_RT", ";", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddHours(8),
                    Secure = true,
                    SameSite = SameSiteMode.None
                });

                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                int start = body.IndexOf("<s:Envelope", StringComparison.OrdinalIgnoreCase);
                int end = body.IndexOf("</s:Envelope>", StringComparison.OrdinalIgnoreCase);

                if (start >= 0 && end > start)
                {
                    string envelopeXml = body.Substring(start, end - start + "</s:Envelope>".Length);

                    XmlSerializer serializer = new XmlSerializer(typeof(RequestEnvelope));
                    using (StringReader reader1 = new StringReader(envelopeXml))
                    {
                        var reqObj = serializer.Deserialize(reader1);
                        if (reqObj is not RequestEnvelope req)
                            return BadRequest("Deserialized object is not a valid RequestEnvelope.");

                        try
                        {
                            var service = HttpContext.RequestServices.GetRequiredService<IMSFSSHTTPService>();
                            var (resp, binaryPayload) = await service.CellStorageRequestNew(req, filePath);

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
                                editorTableBytes = FSSHTTPBResponseBuilder.BuildEmptyQueryChangesResponse();
                            }

                            if (metadataCells.Count > 0)
                            {
                                metadataBytes = FSSHTTPBResponseBuilder.BuildEmptyQueryChangesResponse();
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

                                    int mimePartIndex = 1; // Start at 1 (part 0 is the SOAP envelope)

                                    foreach (var subResponse in response.SubResponse)
                                    {
                                        if (subResponse?.SubResponseData == null)
                                        {
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
                                                    href = $"cid:http://tempuri.org/{mimePartIndex}/{contentId}"
                                                };
                                                subResponse.SubResponseData.Etag = fileInfo.Exists
                                                    ? $"\"{{{Guid.NewGuid().ToString().ToUpper()}}},1\""
                                                    : "\"0\"";
                                                subResponse.SubResponseData.CreateTime = fileInfo.Exists
                                                    ? fileInfo.CreationTimeUtc.Ticks.ToString()
                                                    : "0";
                                                subResponse.SubResponseData.LastModifiedTime = fileInfo.Exists
                                                    ? fileInfo.LastWriteTimeUtc.Ticks.ToString()
                                                    : "0";
                                                subResponse.SubResponseData.ModifiedBy = "Nandaniya Sahil Bharatbhai";
                                                subResponse.SubResponseData.HaveOnlyDemotionChanges = "False";
                                                subResponse.SubResponseData.IsHybridCobalt = "True";
                                                mimePartIndex++;
                                            }
                                            else if (isEditorsTable && editorTableBytes != null)
                                            {
                                                // Editors table: MTOM MIME part without metadata
                                                subResponse.SubResponseData.Include = new Include
                                                {
                                                    href = $"cid:http://tempuri.org/{mimePartIndex}/{contentId}"
                                                };
                                                mimePartIndex++;
                                            }
                                            else if (isMetadata && metadataBytes != null && metadataBytes.Length > 0)
                                            {
                                                // Metadata: no MTOM MIME part, no Include element
                                                // Send FSSHTTPB binary inline as base64-encoded text content
                                                subResponse.SubResponseData.Text = new[] { Convert.ToBase64String(metadataBytes!) };
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
                else
                {
                    return BadRequest("SOAP Envelope not found in request body.");
                }
            }
            else if (method == "PUT")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await Request.Body.CopyToAsync(fs);
                // Aggiorna versione ecc. (TODO)
                return Content($"Uploaded {fileName}", "text/plain");
            }
            else if (method == "LOCK" || method == "UNLOCK")
            {
                // Mock. Implementare gestione lock reale
                return Content($"{method} on {fileName} OK", "text/plain");
            }
            else if (method == "PROPFIND")
            {
                if (!System.IO.File.Exists(filePath))
                    return NotFound();
                var fi = new FileInfo(filePath);
                // Risposta WebDAV minimale (semplificata)
                var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                            <d:multistatus xmlns:d=""DAV:"">
                              <d:response>
                                <d:href>/FSSHTTP/GetDoc/{fileName}</d:href>
                                <d:propstat>
                                  <d:prop>
                                    <d:getcontentlength>{fi.Length}</d:getcontentlength>
                                    <d:getlastmodified>{fi.LastWriteTimeUtc:R}</d:getlastmodified>
                                  </d:prop>
                                  <d:status>HTTP/1.1 200 OK</d:status>
                                </d:propstat>
                              </d:response>
                            </d:multistatus>";
                return Content(xml, "application/xml");
            }

            return BadRequest("Unsupported HTTP method.");
        }
    }
}
