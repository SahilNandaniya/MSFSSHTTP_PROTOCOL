namespace MSFSSHTTP.Middleware
{
    /// <summary>
    /// Intercepts requests whose path contains <c>/_vti_bin/</c> and rewrites them
    /// to an internal route that attribute-routed controllers can match cleanly.
    ///
    /// For example:
    ///   /FSSHTTP/GetDoc/SomeDoc.docx/_vti_bin/cellstorage.svc/CellStorageService
    ///     → rewritten path: /_vti_bin/cellstorage.svc/CellStorageService
    ///
    ///   /FSSHTTP/GetDoc/SomeDoc.docx/_vti_bin/sharedaccess.asmx
    ///     → rewritten path: /_vti_bin/sharedaccess.asmx
    ///
    /// The prefix before <c>/_vti_bin/</c> is stored in <c>HttpContext.Items["VtiBinPrefix"]</c>
    /// so controllers can access it (e.g. to resolve the document path).
    ///
    /// This design is scalable — any new <c>_vti_bin</c> endpoint only needs an attribute route
    /// matching <c>/_vti_bin/...</c>; no routing changes in Program.cs are required.
    /// </summary>
    public class VtiBinRoutingMiddleware
    {
        private readonly RequestDelegate _next;
        private const string VtiBinSegment = "/_vti_bin/";

        public VtiBinRoutingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value;

            if (path != null)
            {
                var vtiBinIndex = path.IndexOf(VtiBinSegment, StringComparison.OrdinalIgnoreCase);

                if (vtiBinIndex >= 0)
                {
                    // Preserve original prefix (everything before /_vti_bin/) for controllers that need it
                    var prefix = path[..vtiBinIndex];
                    context.Items["VtiBinPrefix"] = prefix;

                    // Rewrite path to only the /_vti_bin/... portion
                    var rewrittenPath = path[vtiBinIndex..];
                    context.Request.Path = rewrittenPath;
                }
            }

            return _next(context);
        }
    }

    /// <summary>
    /// Extension method for registering <see cref="VtiBinRoutingMiddleware"/> in the pipeline.
    /// </summary>
    public static class VtiBinRoutingMiddlewareExtensions
    {
        public static IApplicationBuilder UseVtiBinRouting(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<VtiBinRoutingMiddleware>();
        }
    }
}
