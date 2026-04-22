using MSFSSHTTP.Models;
using MSFSSHTTP.Services.SubRequestHandlers;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace MSFSSHTTP.Services
{
    /// <summary>
    /// Builds MTOM/XOP multipart/related responses per MS-FSSHTTP.
    ///
    /// Responsibilities:
    /// - Classifies Cell sub-requests by partition (file contents, editors table, metadata, query access)
    /// - Builds FSSHTTPB binary payloads for each partition
    /// - Patches SubResponseData with Include hrefs or inline base64
    /// - Serializes XML envelope
    /// - Assembles multipart/related MIME response
    /// </summary>
    public class MtomResponseBuilder
    {
        private static readonly Guid EditorsTablePartitionGuid =
            new("7808F4DD-2385-49D6-B7CE-37ACA5E43602");
        private static readonly Guid MetadataPartitionGuid =
            new("383ADC0B-E66E-4438-95E6-E39EF9720122");

        /// <summary>
        /// Builds the complete multipart/related MIME response bytes.
        /// </summary>
        public static byte[] Build(
            RequestEnvelope request,
            ResponseEnvelope response,
            SubRequestContext context,
            string boundary,
            string contentId,
            string requestId)
        {
            // Classify Cell sub-requests by partition
            var allSubRequests = request.Body?.RequestCollection?.Request?
                .SelectMany(r => r.SubRequest ?? Array.Empty<SubRequestElementGenericType>())
                .ToList() ?? new List<SubRequestElementGenericType>();

            var dependentCellSubRequests = allSubRequests
                .Where(sr => sr.Type == SubRequestAttributeType.Cell && !string.IsNullOrEmpty(sr.DependsOn))
                .ToList();

            var fileContentsCells = dependentCellSubRequests
                .Where(sr => string.IsNullOrWhiteSpace(sr.SubRequestData?.PartitionID))
                .ToList();

            var editorTableCells = dependentCellSubRequests
                .Where(sr => string.Equals(sr.SubRequestData?.PartitionID,
                    EditorsTablePartitionGuid.ToString(),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            var metadataCells = dependentCellSubRequests
                .Where(sr => string.Equals(sr.SubRequestData?.PartitionID,
                    MetadataPartitionGuid.ToString(),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            var queryAccessCells = allSubRequests
                .Where(sr => sr.Type == SubRequestAttributeType.Cell
                    && string.IsNullOrEmpty(sr.DependsOn)
                    && sr.SubRequestData?.GetFileProps != true)
                .ToList();

            // Build binaries for each partition
            byte[] fileContentsBytes = Array.Empty<byte>();
            byte[] editorTableBytes = Array.Empty<byte>();
            byte[] metadataBytes = Array.Empty<byte>();
            byte[] queryAccessBytes = Array.Empty<byte>();

            if (fileContentsCells.Count > 0)
            {
                fileContentsBytes = context.BinaryPayload ?? Array.Empty<byte>();
            }

            if (editorTableCells.Count > 0)
            {
                editorTableBytes = FSSHTTPBResponseBuilder.BuildMinimalPartitionResponse(context.WebRootPath);
            }

            if (metadataCells.Count > 0)
            {
                metadataBytes = FSSHTTPBResponseBuilder.BuildEmptyStorageIndexResponse();
            }

            if (queryAccessCells.Count > 0)
            {
                queryAccessBytes = FSSHTTPBResponseBuilder.BuildQueryAccessResponse();
            }

            // Patch SubResponseData with correct Include hrefs or inline base64
            PatchSubResponseData(
                response, dependentCellSubRequests, queryAccessCells,
                fileContentsBytes, editorTableBytes, metadataBytes, queryAccessBytes,
                contentId, requestId);

            // Serialize XML
            string xmlPart = SerializeResponseEnvelope(response);

            // Build multipart MIME response
            return BuildMimeResponse(
                xmlPart, boundary, contentId,
                editorTableBytes, fileContentsBytes);
        }

        private static void PatchSubResponseData(
            ResponseEnvelope response,
            List<SubRequestElementGenericType> dependentCellSubRequests,
            List<SubRequestElementGenericType> queryAccessCells,
            byte[] fileContentsBytes,
            byte[] editorTableBytes,
            byte[] metadataBytes,
            byte[] queryAccessBytes,
            string contentId,
            string requestId)
        {
            if (response.Body?.ResponseCollection?.Response == null)
                return;

            foreach (var resp in response.Body.ResponseCollection.Response)
            {
                if (resp?.SubResponse == null)
                    continue;

                int editorsTableMimeIndex = 1;
                int fileContentsMimeIndex = (editorTableBytes?.Length ?? 0) > 0 ? 2 : 1;

                foreach (var subResponse in resp.SubResponse)
                {
                    if (subResponse?.SubResponseData == null)
                        continue;

                    var cellSubReq = dependentCellSubRequests
                        .FirstOrDefault(sr => sr.SubRequestToken == subResponse.SubRequestToken);

                    var queryAccessSubReq = queryAccessCells
                        .FirstOrDefault(sr => sr.SubRequestToken == subResponse.SubRequestToken);

                    if (cellSubReq == null && queryAccessSubReq == null)
                        continue;

                    if (queryAccessSubReq != null && queryAccessBytes.Length > 0)
                    {
                        subResponse.SubResponseData.Text = new[] { Convert.ToBase64String(queryAccessBytes) };
                    }
                    else if (cellSubReq != null)
                    {
                        bool isFileContents = string.IsNullOrWhiteSpace(cellSubReq.SubRequestData?.PartitionID);
                        bool isMetadata = string.Equals(cellSubReq.SubRequestData?.PartitionID,
                            MetadataPartitionGuid.ToString(),
                            StringComparison.OrdinalIgnoreCase);
                        bool isEditorsTable = string.Equals(cellSubReq.SubRequestData?.PartitionID,
                            EditorsTablePartitionGuid.ToString(),
                            StringComparison.OrdinalIgnoreCase);

                        if (isFileContents && fileContentsBytes != null)
                        {
                            subResponse.SubResponseData.Include = new Include
                            {
                                href = $"cid:http://tempuri.org/{fileContentsMimeIndex}/{contentId}"
                            };
                            subResponse.ServerCorrelationId = requestId;
                        }
                        else if (isEditorsTable && editorTableBytes != null)
                        {
                            subResponse.SubResponseData.Include = new Include
                            {
                                href = $"cid:http://tempuri.org/{editorsTableMimeIndex}/{contentId}"
                            };
                        }
                        else if (isMetadata && metadataBytes != null && metadataBytes.Length > 0)
                        {
                            subResponse.SubResponseData.Text = new[] { Convert.ToBase64String(metadataBytes) };
                            subResponse.ServerCorrelationId = requestId;
                        }
                    }
                }
            }
        }

        private static string SerializeResponseEnvelope(ResponseEnvelope response)
        {
            var xmlWriterSettings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Encoding = Encoding.UTF8,
                Indent = false
            };
            var ns = new XmlSerializerNamespaces();
            ns.Add("s", "http://schemas.xmlsoap.org/soap/envelope/");
            ns.Add("", "");

            using var sw = new StringWriter();
            using var xw = XmlWriter.Create(sw, xmlWriterSettings);
            var serializer = new XmlSerializer(typeof(ResponseEnvelope));
            serializer.Serialize(xw, response, ns);
            return sw.ToString();
        }

        private static byte[] BuildMimeResponse(
            string xmlPart,
            string boundary,
            string contentId,
            byte[] editorTableBytes,
            byte[] fileContentsBytes)
        {
            var sb = new StringBuilder();
            sb.Append($"--{boundary}\r\n");
            sb.Append("Content-ID: <http://tempuri.org/0>\r\n");
            sb.Append("Content-Transfer-Encoding: 8bit\r\n");
            sb.Append("Content-Type: application/xop+xml; charset=utf-8; type=\"text/xml\"\r\n");
            sb.Append("\r\n");
            sb.Append(xmlPart);

            var responseBytes = new List<byte>();
            responseBytes.AddRange(Encoding.UTF8.GetBytes(sb.ToString()));

            // Add editors table MIME part (if present)
            if (editorTableBytes?.Length > 0)
            {
                var headers = new StringBuilder();
                headers.Append($"\r\n--{boundary}\r\n");
                headers.Append($"Content-ID: <http://tempuri.org/1/{contentId}>\r\n");
                headers.Append("Content-Transfer-Encoding: binary\r\n");
                headers.Append("Content-Type: application/octet-stream\r\n");
                headers.Append("\r\n");

                responseBytes.AddRange(Encoding.UTF8.GetBytes(headers.ToString()));
                responseBytes.AddRange(editorTableBytes);
            }

            // Add file contents MIME part (if present)
            if (fileContentsBytes?.Length > 0)
            {
                int fileContentMimeIndex = (editorTableBytes?.Length ?? 0) > 0 ? 2 : 1;

                var headers = new StringBuilder();
                headers.Append($"\r\n--{boundary}\r\n");
                headers.Append($"Content-ID: <http://tempuri.org/{fileContentMimeIndex}/{contentId}>\r\n");
                headers.Append("Content-Transfer-Encoding: binary\r\n");
                headers.Append("Content-Type: application/octet-stream\r\n");
                headers.Append("\r\n");

                responseBytes.AddRange(Encoding.UTF8.GetBytes(headers.ToString()));
                responseBytes.AddRange(fileContentsBytes);
            }

            // Closing boundary
            responseBytes.AddRange(Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n"));

            return responseBytes.ToArray();
        }
    }
}
