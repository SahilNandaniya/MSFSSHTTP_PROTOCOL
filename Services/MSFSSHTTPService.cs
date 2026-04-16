//using FSSHTTPandWOPIInspector.Parsers;
using MSFSSHTTP.Models;
using MSFSSHTTP.Parsers;
using MSFSSHTTP.Utilities;

namespace MSFSSHTTP.Services
{
    public class MSFSSHTTPService : IMSFSSHTTPService
    {
        public string SchemaLockId { get; set; }

        /// <summary>
        /// Binary payload generated for the first Cell/QueryChanges sub-request.
        /// Stored during processing and returned alongside the envelope.
        /// </summary>
        private byte[] _binaryPayload;

        public Task<(ResponseEnvelope Envelope, byte[] BinaryPayload)> CellStorageRequestNew(RequestEnvelope request, string filePath)
        {
            _binaryPayload = null;

            var requestCollection = request.Body?.RequestCollection;
            if (requestCollection?.Request == null || requestCollection.Request.Length == 0)
            {
                return Task.FromResult((BuildErrorResponse("No requests in collection."), (byte[])null));
            }

            var responses = new List<Response>();

            foreach (var req in requestCollection.Request)
            {
                var subResponses = new List<SubResponseElementGenericType>();

                if (req.SubRequest != null)
                {
                    foreach (var subReq in req.SubRequest)
                    {
                        var subResponse = ProcessSubRequest(subReq, filePath);
                        subResponses.Add(subResponse);
                    }
                }

                responses.Add(new Response
                {
                    Url = req.Url,
                    UrlIsEncoded = "False",
                    RequestToken = req.RequestToken,
                    HealthScore = "2",
                    RequestedClientImpact = 3,
                    IntervalOverride = "0",
                    ExpectedAccessRead = true,
                    ExpectedAccessWrite = true,
                    ResourceID = GlobalConstant.StableDocumentId.ToString("N"),
                    ErrorCode = GenericErrorCodeTypes.Success,
                    ErrorCodeSpecified = false,
                    SubResponse = subResponses.ToArray(),
                    TenantId = "e777e544-630e-475c-a07a-cd4143ce5140"
                });
            }

            var envelope = new ResponseEnvelope
            {
                Body = new ResponseEnvelopeBody
                {
                    ResponseVersion = new ResponseVersion
                    {
                        Version = 2,
                        MinorVersion = 3
                    },
                    ResponseCollection = new ResponseCollection
                    {
                        //WebUrl = requestCollection.Request[0]?.Url ?? "",
                        WebUrl = "https://pnq1-lhp-n91356/FSSHTTP/GetDoc/",
                        WebUrlIsEncoded = "False",
                        Response = responses.ToArray()
                    }
                }
            };

            return Task.FromResult((envelope, _binaryPayload));
        }

        private SubResponseElementGenericType ProcessSubRequest(SubRequestElementGenericType subReq, string filePath)
        {
            switch (subReq.Type)
            {
                case SubRequestAttributeType.Coauth:
                    SchemaLockId = subReq.SubRequestData.SchemaLockID;
                    return BuildCoAuthResponse(subReq);

                case SubRequestAttributeType.ExclusiveLock:
                    return BuildExclusiveLockResponse(subReq);

                case SubRequestAttributeType.ServerTime:
                    return BuildServerTimeResponse(subReq.SubRequestToken);

                case SubRequestAttributeType.WhoAmI:
                    return BuildWhoAmIResponse(subReq.SubRequestToken);

                case SubRequestAttributeType.Cell:
                    {
                        var cellData = subReq.SubRequestData;

                        if (cellData != null && cellData.GetFileProps)
                        {
                            return BuildCellFilePropsResponse(subReq, filePath);
                        }

                        return BuildCellResponse(subReq, filePath);
                    }
                case SubRequestAttributeType.EditorsTable:
                    return BuildEmptySuccessResponse(subReq.SubRequestToken);

                case SubRequestAttributeType.GetDocMetaInfo:
                    return BuildGetDocMetaInfoResponse(subReq, filePath);

                case SubRequestAttributeType.Label:
                    return BuildFeatureDisabledOnServerTenantNotSupportedResponse(subReq.SubRequestToken);

                case SubRequestAttributeType.SchemaLock:
                    return BuildDependentOnlyOnNotSupportedRequestGetSupported(subReq.SubRequestToken);

                case SubRequestAttributeType.GetVersions:
                    return BuildVersionNotSupported(subReq.SubRequestToken);

                default:
                    return BuildNotSupportedResponse(subReq.SubRequestToken);
            }
        }

        private SubResponseElementGenericType BuildVersionNotSupported(string subRequestToken)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "2147500037",
                GetVersionsResponse = new GetVersionsResponseType()
                {
                    GetVersionsResult = new GetVersionsResult()
                    {
                        results = new Models.Results
                        {
                            list = new ResultsList() { id = "00000000-0000-0000-0000-000000000000" },
                            versioning = new ResultsVersioning() { enabled = 0 },
                            settings = new ResultsSettings() { url = "" },
                        }
                    }
                }
            };
        }

        private SubResponseElementGenericType BuildDependentOnlyOnNotSupportedRequestGetSupported(string subRequestToken)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequestToken,
                ErrorCode = GenericErrorCodeTypes.DependentOnlyOnNotSupportedRequestGetSupported.ToString(),
                HResult = "2147500037",
                SubResponseData = new SubResponseDataGenericType()
            };
        }

        private SubResponseElementGenericType BuildFeatureDisabledOnServerTenantNotSupportedResponse(string subRequestToken)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequestToken,
                ErrorCode = GenericErrorCodeTypes.FeatureDisabledOnServerTenantNotSupported.ToString(),
                HResult = "2147500037",
                SubResponseData = new SubResponseDataGenericType()
            };
        }

        private SubResponseElementGenericType BuildNotSupportedResponse(string subRequestToken)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType()
            };
        }

        private SubResponseElementGenericType BuildEmptySuccessResponse(string subRequestToken)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType()
            };
        }

        private SubResponseElementGenericType BuildExclusiveLockResponse(SubRequestElementGenericType subReq)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subReq.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType
                {
                    CoauthStatus = CoauthStatusType.Alone,
                    CoauthStatusSpecified = true,
                    TransitionID = Guid.NewGuid().ToString()
                }
            };
        }

        private SubResponseElementGenericType BuildServerTimeResponse(string subRequestToken)
        {
            var serverTimeTicks = DateTimeOffset.UtcNow.Ticks;
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType
                {
                    ServerTime = serverTimeTicks.ToString()
                }
            };
        }

        private SubResponseElementGenericType BuildWhoAmIResponse(string subRequestToken)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType
                {
                    UserName = "Nandaniya Sahil Bharatbhai",
                    UserEmailAddress = "sahil.bharatbhai@anaplan.com",
                    UserIsAnonymous = false,
                    UserIsAnonymousSpecified = true,
                    UserLogin = "sahil.bharatbhai@anaplan.com",
                    UserSIPAddress = "sahil.bharatbhai@anaplan.com"
                }
            };
        }

        private SubResponseElementGenericType BuildCoAuthResponse(SubRequestElementGenericType subRequest)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subRequest.SubRequestToken,
                HResult = "0",
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                SubResponseData = new SubResponseDataGenericType
                {
                    CoauthStatus = CoauthStatusType.Alone,
                    LockType = LockTypes.SchemaLock.ToString(),
                    LockTypeSpecified = true,
                    CoauthStatusSpecified = true,
                    TransitionID = GlobalConstant.StableDocumentId.ToString()
                }
            };
        }

        /// <summary>
        /// Reads the Cell sub-request FSSHTTPB package (base64 in Text) and returns the RequestID
        /// of the QueryChanges sub-request (0x02). Word matches SubResponse RequestID to the request;
        /// a mismatch causes the client to reject the binary payload.
        /// </summary>
        private static ulong TryGetQueryChangesRequestId(SubRequestElementGenericType subReq)
        {
            try
            {
                var textArr = subReq.SubRequestData?.Text;
                if (textArr == null || textArr.Length == 0 || string.IsNullOrEmpty(textArr[0]))
                    return 1;

                var bytes = Convert.FromBase64String(textArr[0]);
                var fssReq = new FsshttpbRequest();
                fssReq.Parse(new MemoryStream(bytes));
                if (fssReq.SubRequest == null)
                    return 1;

                foreach (var sreq in fssReq.SubRequest)
                {
                    if (sreq.RequestType.GetUint(sreq.RequestType) == 0x02)
                        return sreq.RequestID.GetUint(sreq.RequestID);
                }
            }
            catch
            {
                // Ignore parse errors; use default RequestID.
            }

            return 1;
        }

        private SubResponseElementGenericType BuildCellFilePropsResponse(
SubRequestElementGenericType subReq,
    string filePath)
        {
            var fi = new FileInfo(filePath);

            long lastModTicks = fi.Exists ? fi.LastWriteTimeUtc.ToFileTimeUtc() : 0;
            long createTicks = fi.Exists ? fi.CreationTimeUtc.ToFileTimeUtc() : 0;

            var etag = fi.Exists
                ? $"\"{{{GlobalConstant.StableDocumentId.ToString().ToUpper()}}},1,irm24A4B01E53DC46C79894148DC0051E64\""
                : "\"0\"";

            // Check if file exists and generate FSSHTTPB binary response
            if (System.IO.File.Exists(filePath) && _binaryPayload == null)
            {
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var storageGuid = Guid.NewGuid();
                ulong requestID = TryGetQueryChangesRequestId(subReq);
                _binaryPayload = FSSHTTPBResponseBuilder.BuildQueryChangesResponse(fileBytes, storageGuid, requestID);
            }

            return new SubResponseElementGenericType
            {
                SubRequestToken = subReq.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType
                {
                    Etag = etag,
                    CreateTime = createTicks.ToString(),
                    LastModifiedTime = lastModTicks.ToString(),
                    ModifiedBy = "Sahil Nandaniya",
                    HaveOnlyDemotionChanges = "False",
                    IsHybridCobalt = "True",
                }
            };
        }

        private SubResponseElementGenericType BuildCellResponse(SubRequestElementGenericType subReq, string filePath)
        {
            return new SubResponseElementGenericType
            {
                SubRequestToken = subReq.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType()
            };
        }

        private SubResponseElementGenericType BuildGetDocMetaInfoResponse(SubRequestElementGenericType subReq, string filePath)
        {
            var fi = new FileInfo(filePath);
            var now = DateTime.UtcNow;
            var created = fi.Exists ? fi.CreationTimeUtc : now;
            var modified = fi.Exists ? fi.LastWriteTimeUtc : now;
            //var etag = fi.Exists ? $"\"{fi.LastWriteTimeUtc.Ticks:x}\"" : "\"0\"";
            //var etag = GlobalConstant.StableDocumentId.ToString().ToUpper();
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
                    new GetDocMetaInfoPropertyType { Key = "vti_sourcecontrollockid", Value = SchemaLockId},
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
                SubRequestToken = subReq.SubRequestToken,
                ErrorCode = GenericErrorCodeTypes.Success.ToString(),
                HResult = "0",
                SubResponseData = new SubResponseDataGenericType
                {
                    DocProps = docProps,
                    FolderProps = folderProps
                }
            };
        }

        private ResponseEnvelope BuildErrorResponse(string errorMessage)
        {
            return new ResponseEnvelope
            {
                Body = new ResponseEnvelopeBody
                {
                    ResponseVersion = new ResponseVersion
                    {
                        Version = 2,
                        MinorVersion = 2,
                        ErrorCode = GenericErrorCodeTypes.InvalidSubRequest,
                        ErrorCodeSpecified = true,
                        ErrorMessage = errorMessage
                    }
                }
            };
        }
    }
}
