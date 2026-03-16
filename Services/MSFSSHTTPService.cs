//using FSSHTTPandWOPIInspector.Parsers;
using MSFSSHTTP.Models;

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
                    HealthScore = "0",
                    RequestedClientImpact = 3,
                    IntervalOverride = "0",
                    ExpectedAccessRead = true,
                    ExpectedAccessWrite = false,
                    ResourceID = Guid.NewGuid().ToString(),
                    ErrorCode = GenericErrorCodeTypes.Success,
                    ErrorCodeSpecified = false,
                    SubResponse = subResponses.ToArray()
                });
            }

            var envelope = new ResponseEnvelope
            {
                Body = new ResponseEnvelopeBody
                {
                    ResponseVersion = new ResponseVersion
                    {
                        Version = 2,
                        MinorVersion = 2
                    },
                    ResponseCollection = new ResponseCollection
                    {
                        WebUrl = requestCollection.Request[0]?.Url ?? "",
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

                default:
                    return BuildNotSupportedResponse(subReq.SubRequestToken);
            }
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
                    TransitionID = Guid.NewGuid().ToString()
                    // t specifies the unique file identifier stored for that file on the
                    //protocol server.- fileID probably
                }
            };
        }

        private SubResponseElementGenericType BuildCellFilePropsResponse(
SubRequestElementGenericType subReq,
    string filePath)
        {
            var fi = new FileInfo(filePath);

            long lastModTicks = fi.Exists ? fi.LastWriteTimeUtc.Ticks : 0;
            long createTicks = fi.Exists ? fi.CreationTimeUtc.Ticks : 0;

            var etag = fi.Exists
                ? $"\"{{{Guid.NewGuid().ToString().ToUpper()}}},1\""
                : "\"0\"";

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
                    IsHybridCobalt = "False",
                }
            };
        }

        private SubResponseElementGenericType BuildCellResponse(SubRequestElementGenericType subReq, string filePath)
        {
            // Check if file exists and generate FSSHTTPB binary response
            if (System.IO.File.Exists(filePath) && _binaryPayload == null)
            {
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var storageGuid = Guid.NewGuid();
                _binaryPayload = FSSHTTPBResponseBuilder.BuildQueryChangesResponse(fileBytes, storageGuid);
            }

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
            var etag = fi.Exists ? $"\"{fi.LastWriteTimeUtc.Ticks:x}\"" : "\"0\"";
            var fileSize = fi.Exists ? fi.Length.ToString() : "0";
            var docId = Guid.NewGuid().ToString().ToUpper();
            var listId = Guid.NewGuid().ToString().ToUpper();
            var parentId = Guid.NewGuid().ToString().ToUpper();

            var docProps = new GetDocMetaInfoPropertySetType
            {
                Property = new[]
                {
                    new GetDocMetaInfoPropertyType { Key = "vti_timecreated", Value = created.ToString("yyyy-MM-ddTHH:mm:ss") },
                    new GetDocMetaInfoPropertyType { Key = "vti_timelastmodified", Value = modified.ToString("yyyy-MM-ddTHH:mm:ss") },
                    new GetDocMetaInfoPropertyType { Key = "vti_filesize", Value = fileSize },
                    new GetDocMetaInfoPropertyType { Key = "vti_etag", Value = etag },
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
                    new GetDocMetaInfoPropertyType { Key = "vti_sourcecontrollockid", Value = SchemaLockId}

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
