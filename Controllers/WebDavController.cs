using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MSFSSHTTP.Controllers
{
    /// <summary>
    /// Handles WebDAV fallback methods for document access.
    /// Supports: GET, HEAD, OPTIONS, PUT, LOCK, UNLOCK, PROPFIND.
    ///
    /// This controller is matched via conventional routing as the lowest-priority catch-all
    /// for requests that do not match any MS-FSSHTTP attribute-routed endpoint
    /// (CellStorageController, SharedAccessController, etc.).
    /// </summary>
    public class WebDavController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public WebDavController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [AcceptVerbs("GET", "HEAD", "LOCK", "OPTIONS", "PROPFIND", "PUT", "UNLOCK")]
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
            else if (method == "PUT")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await Request.Body.CopyToAsync(fs);
                // TODO: Update version tracking
                return Content($"Uploaded {fileName}", "text/plain");
            }
            else if (method == "LOCK" || method == "UNLOCK")
            {
                // Mock. TODO: Implement real lock management
                return Content($"{method} on {fileName} OK", "text/plain");
            }
            else if (method == "PROPFIND")
            {
                if (!System.IO.File.Exists(filePath))
                    return NotFound();
                var fi = new FileInfo(filePath);
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
