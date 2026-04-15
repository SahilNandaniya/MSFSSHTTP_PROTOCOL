using System.Text;

namespace MSFSSHTTP.Utilities
{
    /// <summary>
    /// Parses multipart/related (MTOM) bodies and maps Content-ID headers to part payloads.
    /// Used to resolve xop:Include references in SOAP Cell SubRequestData.
    /// </summary>
    public static class MtomMultipartParser
    {
        /// <summary>
        /// If the request is multipart, parses parts and returns a lookup of normalized Content-ID to binary body.
        /// Returns false if Content-Type is not multipart or boundary is missing.
        /// </summary>
        public static bool TryParseMultipart(string? contentType, byte[] body, out Dictionary<string, byte[]> contentIdToBytes)
        {
            contentIdToBytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(contentType) ||
                contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var boundary = ExtractBoundary(contentType);
            if (string.IsNullOrEmpty(boundary))
            {
                return false;
            }

            var boundaryBytes = Encoding.ASCII.GetBytes("--" + boundary);
            int pos = IndexOfSequence(body, boundaryBytes, 0);
            if (pos < 0)
            {
                return false;
            }

            pos += boundaryBytes.Length;
            // Skip optional whitespace / CRLF after first boundary
            while (pos < body.Length && (body[pos] == '\r' || body[pos] == '\n'))
            {
                pos++;
            }

            while (pos < body.Length)
            {
                var headerEnd = FindHeaderBodySplit(body, pos);
                if (headerEnd < 0)
                {
                    break;
                }

                var headerText = Encoding.UTF8.GetString(body, pos, headerEnd - pos);
                pos = headerEnd + 4; // \r\n\r\n

                string? contentId = null;
                foreach (var line in headerText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                {
                    if (line.StartsWith("Content-ID:", StringComparison.OrdinalIgnoreCase))
                    {
                        contentId = line["Content-ID:".Length..].Trim();
                        break;
                    }
                }

                var nextBoundary = IndexOfSequence(body, boundaryBytes, pos);
                if (nextBoundary < 0)
                {
                    break;
                }

                int bodyLen = nextBoundary - pos;
                if (bodyLen >= 2 && body[pos + bodyLen - 2] == '\r' && body[pos + bodyLen - 1] == '\n')
                {
                    bodyLen -= 2;
                }
                else if (bodyLen >= 1 && body[pos + bodyLen - 1] == '\n')
                {
                    bodyLen -= 1;
                }

                var partBytes = new byte[bodyLen];
                if (bodyLen > 0)
                {
                    Buffer.BlockCopy(body, pos, partBytes, 0, bodyLen);
                }

                if (!string.IsNullOrEmpty(contentId))
                {
                    foreach (var key in NormalizeContentIds(contentId))
                    {
                        if (!contentIdToBytes.ContainsKey(key))
                        {
                            contentIdToBytes[key] = partBytes;
                        }
                    }
                }

                pos = nextBoundary + boundaryBytes.Length;
                if (pos + 1 < body.Length && body[pos] == '-' && body[pos + 1] == '-')
                {
                    break;
                }

                while (pos < body.Length && (body[pos] == '\r' || body[pos] == '\n'))
                {
                    pos++;
                }
            }

            return contentIdToBytes.Count > 0;
        }

        private static string? ExtractBoundary(string contentType)
        {
            const string key = "boundary=";
            int i = contentType.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
            {
                return null;
            }

            i += key.Length;
            if (i >= contentType.Length)
            {
                return null;
            }

            if (contentType[i] == '"')
            {
                i++;
                int end = contentType.IndexOf('"', i);
                return end < 0 ? null : contentType[i..end];
            }

            int semi = contentType.IndexOf(';', i);
            return semi < 0 ? contentType[i..].Trim() : contentType[i..semi].Trim();
        }

        private static int FindHeaderBodySplit(byte[] data, int start)
        {
            for (int i = start; i + 3 < data.Length; i++)
            {
                if (data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' && data[i + 3] == '\n')
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfSequence(byte[] haystack, byte[] needle, int start)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length)
            {
                return -1;
            }

            for (int i = start; i <= haystack.Length - needle.Length; i++)
            {
                int j = 0;
                for (; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        break;
                    }
                }

                if (j == needle.Length)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Produces lookup keys for a Content-ID header value and href-style cid: references.
        /// </summary>
        public static IEnumerable<string> NormalizeContentIds(string contentIdOrHref)
        {
            var s = contentIdOrHref.Trim();
            if (s.StartsWith('<') && s.EndsWith('>'))
            {
                s = s[1..^1];
            }

            yield return s;
            yield return "<" + s + ">";

            if (s.StartsWith("cid:", StringComparison.OrdinalIgnoreCase))
            {
                var rest = s[4..];
                yield return rest;
                yield return "<" + rest + ">";
            }
            else
            {
                var withCid = "cid:" + s;
                yield return withCid;
                yield return "<" + withCid + ">";
            }
        }
    }
}
