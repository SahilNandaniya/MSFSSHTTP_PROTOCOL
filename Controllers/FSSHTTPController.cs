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
            string fileName = "TestStruttura1.docx";  //ATTENTION: Hardcoded for testing purposes

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
                            var resp = await service.CellStorageRequestNew(req, filePath);

                            var firstPartitionCellToken = req.Body?.RequestCollection?.Request?
                                .SelectMany(r => r.SubRequest ?? Array.Empty<SubRequestElementGenericType>())
                                .Where(sr => sr.Type == SubRequestAttributeType.Cell)
                                .Where(sr => !string.IsNullOrWhiteSpace(sr.SubRequestData?.PartitionID))
                                .Select(sr => sr.SubRequestToken)
                                .FirstOrDefault();

                            var cellTokens = req.Body?.RequestCollection?.Request?
                                .SelectMany(r => r.SubRequest ?? Array.Empty<SubRequestElementGenericType>())
                                .Where(sr => sr.Type == SubRequestAttributeType.Cell)
                                //.Where(sr => !string.IsNullOrWhiteSpace(sr.SubRequestData.PartitionID))
                                .Select(sr => sr.SubRequestToken)
                                .ToHashSet() ?? new HashSet<string>();

                            if (resp.Body?.ResponseCollection?.Response != null && cellTokens.Count > 0)
                            {
                                foreach (var response in resp.Body.ResponseCollection.Response)
                                {
                                    if (response?.SubResponse == null)
                                    {
                                        continue;
                                    }

                                    int index = 1;
                                    foreach (var subResponse in response.SubResponse)
                                    {
                                        if (subResponse?.SubResponseData == null || !cellTokens.Contains(subResponse.SubRequestToken))
                                        {
                                            continue;
                                        }

                                        subResponse.SubResponseData.Include = null;

                                        if (!string.IsNullOrWhiteSpace(firstPartitionCellToken) && subResponse.SubRequestToken == firstPartitionCellToken)
                                        {
                                            subResponse.SubResponseData.Include = new Include
                                            {
                                                href = $"cid:http://tempuri.org/{index++}/{contentId}"
                                            };
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
                            //ns.Add("xop", "http://www.w3.org/2004/08/xop/include");
                            ns.Add("", ""); // Default for other elements

                            string xmlPart;
                            using (var sw = new StringWriter())
                            using (var xw = XmlWriter.Create(sw, xmlWriterSettings))
                            {
                                var respSerializer = new XmlSerializer(typeof(ResponseEnvelope));
                                respSerializer.Serialize(xw, resp, ns);
                                xmlPart = sw.ToString();
                            }

                            // Read file stream
                            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

                            // Build multipart response
                            var sb = new StringBuilder();
                            sb.AppendLine($"--{boundary}");
                            sb.AppendLine($"Content-ID: <http://tempuri.org/0>");
                            sb.AppendLine("Content-Transfer-Encoding: 8bit");
                            sb.AppendLine("Content-Type: application/xop+xml; charset=utf-8; type=\"text/xml\"");
                            sb.AppendLine();
                            sb.AppendLine(xmlPart);

                            // Convert XML part to bytes
                            var xmlBytes = Encoding.UTF8.GetBytes(sb.ToString());

                            // End boundary
                            var endBoundary = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");

                            // Combine all parts
                            var responseBytes = new List<byte>();
                            responseBytes.AddRange(xmlBytes);

                            if (!string.IsNullOrWhiteSpace(firstPartitionCellToken))
                            {
                                var attachmentHeaders = new StringBuilder();
                                attachmentHeaders.AppendLine($"--{boundary}");
                                attachmentHeaders.AppendLine($"Content-ID: <{xopContentId}>");
                                attachmentHeaders.AppendLine("Content-Transfer-Encoding: binary");
                                attachmentHeaders.AppendLine("Content-Type: application/octet-stream");
                                attachmentHeaders.AppendLine();

                                responseBytes.AddRange(Encoding.UTF8.GetBytes(attachmentHeaders.ToString()));
                                responseBytes.AddRange(fileBytes);
                            }

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
