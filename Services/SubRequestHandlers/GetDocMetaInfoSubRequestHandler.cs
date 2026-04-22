using MSFSSHTTP.Models;
using MSFSSHTTP.Utilities;

namespace MSFSSHTTP.Services.SubRequestHandlers
{
    /// <summary>
    /// Handles GetDocMetaInfo sub-requests.
    /// Per MS-FSSHTTP Section 2.3.1.26 / 3.1.4.9.
    /// </summary>
    public class GetDocMetaInfoSubRequestHandler : ISubRequestHandler
    {
        public SubRequestAttributeType HandledType => SubRequestAttributeType.GetDocMetaInfo;

        public SubResponseElementGenericType Handle(SubRequestElementGenericType subRequest, SubRequestContext context)
        {
            var fi = context.FileInfo;
            var now = DateTime.UtcNow;
            var created = fi.Exists ? fi.CreationTimeUtc : now;
            var modified = fi.Exists ? fi.LastWriteTimeUtc : now;
            var fileSize = fi.Exists ? fi.Length.ToString() : "0";
            var docId = GlobalConstant.StableDocumentId.ToString().ToUpper();
            var listId = Guid.NewGuid().ToString().ToUpper();
            var parentId = Guid.NewGuid().ToString().ToUpper();

            var docProps = new GetDocMetaInfoPropertySetType
            {
                Property = new[]
                {
                    new GetDocMetaInfoPropertyType { Key = "vti_timecreated", Value = created.ToString("yyyy-MM-ddTHH:mm:ss") },
                    new GetDocMetaInfoPropertyType { Key = "vti_timelastmodified", Value = modified.ToString("yyyy-MM-ddTHH:mm:ss") },
                    new GetDocMetaInfoPropertyType { Key = "vti_filesize", Value = fileSize },
                    new GetDocMetaInfoPropertyType { Key = "vti_etag", Value = $"&quot;{{{docId}}},1&quot;" },
                    new GetDocMetaInfoPropertyType { Key = "vti_author", Value = "sahil.bharatbhai@anaplan.com" },
                    new GetDocMetaInfoPropertyType { Key = "vti_modifiedby", Value = "sahil.bharatbhai@anaplan.com" },
                    new GetDocMetaInfoPropertyType { Key = "vti_docstoreversion", Value = "1" },
                    new GetDocMetaInfoPropertyType { Key = "vti_contenttag", Value = $"{{{docId}}},1,1" },
                    new GetDocMetaInfoPropertyType { Key = "vti_docstoretype", Value = "0" },
                    new GetDocMetaInfoPropertyType { Key = "vti_listid", Value = $"{{{listId}}}" },
                    new GetDocMetaInfoPropertyType { Key = "vti_listbasetype", Value = "1" },
                    new GetDocMetaInfoPropertyType { Key = "vti_listservertemplate", Value = "700" },
                    new GetDocMetaInfoPropertyType { Key = "vti_listtitle", Value = "Documents" },
                    new GetDocMetaInfoPropertyType { Key = "vti_folderitemcount", Value = "0" },
                    new GetDocMetaInfoPropertyType { Key = "vti_level", Value = "1" },
                    new GetDocMetaInfoPropertyType { Key = "vti_parentid", Value = $"{{{parentId}}}" },
                    new GetDocMetaInfoPropertyType { Key = "vti_replid", Value = $"rid:{{{docId}}}" },
                    new GetDocMetaInfoPropertyType { Key = "vti_sourcecontrollockid", Value = context.SchemaLockId },
                    new GetDocMetaInfoPropertyType { Key = "vti_sourcecontrolcheckedoutby", Value = "V2.0" }
                }
            };

            var folderProps = new GetDocMetaInfoPropertySetType
            {
                Property = new[]
                {
                    new GetDocMetaInfoPropertyType { Key = "vti_docstoretype", Value = "1" },
                    new GetDocMetaInfoPropertyType { Key = "vti_listbasetype", Value = "1" },
                    new GetDocMetaInfoPropertyType { Key = "vti_listservertemplate", Value = "700" },
                    new GetDocMetaInfoPropertyType { Key = "vti_listtitle", Value = "Documents" },
                    new GetDocMetaInfoPropertyType { Key = "vti_folderitemcount", Value = "0" },
                    new GetDocMetaInfoPropertyType { Key = "vti_level", Value = "1" },
                    new GetDocMetaInfoPropertyType { Key = "vti_parentid", Value = $"{{{parentId}}}" },
                    new GetDocMetaInfoPropertyType { Key = "vti_replid", Value = $"rid:{{{parentId}}}" }
                }
            };

            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequest.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType
                {
                    DocProps = docProps,
                    FolderProps = folderProps
                }
            };
        }
    }
}
