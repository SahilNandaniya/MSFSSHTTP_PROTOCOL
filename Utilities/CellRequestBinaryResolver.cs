using MSFSSHTTP.Models;

namespace MSFSSHTTP.Utilities
{
    /// <summary>
    /// Resolves FSSHTTPB request bytes for a SOAP Cell sub-request from inline base64 or MTOM Include.
    /// </summary>
    public static class CellRequestBinaryResolver
    {
        /// <summary>
        /// Attempts to obtain raw FSSHTTPB bytes for a Cell sub-request.
        /// </summary>
        /// <param name="sr">SOAP Cell sub-request (not GetFileProps).</param>
        /// <param name="mtomParts">Optional map from normalized Content-ID to part bytes (from MTOM).</param>
        /// <param name="bytes">Decoded FSSHTTPB request payload.</param>
        public static bool TryGetFsshttpbRequestBytes(
            SubRequestElementGenericType sr,
            IReadOnlyDictionary<string, byte[]>? mtomParts,
            out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (sr.SubRequestData == null)
            {
                return false;
            }

            var text = sr.SubRequestData.Text;
            if (text != null && text.Length > 0 && !string.IsNullOrEmpty(text[0]))
            {
                try
                {
                    bytes = Convert.FromBase64String(text[0].Trim());
                    return bytes.Length > 0;
                }
                catch (FormatException)
                {
                    // Fall through to Include
                }
            }

            var href = sr.SubRequestData.Include?.href;
            if (string.IsNullOrEmpty(href) || mtomParts == null || mtomParts.Count == 0)
            {
                return false;
            }

            foreach (var key in MtomMultipartParser.NormalizeContentIds(href))
            {
                if (mtomParts.TryGetValue(key, out var part) && part.Length > 0)
                {
                    bytes = part;
                    return true;
                }
            }

            return false;
        }
    }
}
