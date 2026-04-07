using MSFSSHTTP.Parsers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace MSFSSHTTP.Services
{
    /// <summary>
    /// Builds bit-perfect MS-FSSHTTPB binary responses per MS-FSSHTTPB and MS-FSSHTTPD.
    /// Implements QueryChanges response with ZIP-based file chunking for .docx files.
    /// </summary>
    public static class FSSHTTPBResponseBuilder
    {
        // Well-known GUIDs from MS-FSSHTTPB / MS-FSSHTTPD
        private static readonly Guid CellKnowledgeGuid = new("327A35F6-0761-4414-9686-51E900667A4D");
        private static readonly Guid WaterlineKnowledgeGuid = new("3A76E90E-8032-4D0C-B9DD-F3C65029433E");
        private static readonly Guid ContentTagKnowledgeGuid = new("10091F13-C882-40FB-9886-6533F934C21D");

        // Well-known schema GUID for CoBalt storage                
        private static readonly Guid StorageManifestSchemaGuid = new("0EB93394-571D-41E9-AAD3-880D92D31955");

        // Root Extended GUID – well-known "default cell storage" root
        private static readonly Guid RootGuid = new("84DEFAB9-AAA3-4A0D-A3A8-520C77AC7073");
        private static readonly Guid CellGuid1 = new("84DEFAB9-AAA3-4A0D-A3A8-520C77AC7073");
        private static readonly Guid CellGuid2 = new("6F2A4665-42C8-46C7-BAB4-E28FDCE1E32B");

        /// <summary>
        /// Build a full FSSHTTPB binary QueryChanges response for the given file.
        /// Returns the serialized bytes ready to be embedded as XOP binary attachment.
        /// </summary>
        public static byte[] BuildQueryChangesResponse(byte[] fileBytes, Guid storageGuid)
        {
            // Step 1: Chunk the file (ZIP-based chunking per MS-FSSHTTPD Section 2.4.1)
            var chunks = ChunkFile(fileBytes);

            // Step 2: Assign Extended GUIDs to all data elements
            uint guidCounter = 1;
            ulong uniqueSignatureCounter = 1;
            var storageIndexGuid = NextExGuid(storageGuid, ref guidCounter);
            var storageManifestGuid = NextExGuid(storageGuid, ref guidCounter);
            var cellManifestGuid = NextExGuid(storageGuid, ref guidCounter);
            var revisionManifestGuid = NextExGuid(storageGuid, ref guidCounter);
            var currentRevisionGuid = NextExGuid(storageGuid, ref guidCounter);
            var baseRevisionGuid = new ExtendedGUIDNullValue { Type = 0x00 };

            // Root object GUID (intermediate node referencing all leaf chunks)
            var rootObjectGuid = NextExGuid(storageGuid, ref guidCounter);
            var rootObjectGroupGuid = NextExGuid(storageGuid, ref guidCounter);

            // Serial number for all data elements
            var serialNumber = new SerialNumber64BitUintValue
            {
                Type = 0x80,
                GUID = storageGuid,
                Value = 1
            };

            // Step 3: Build Data Elements
            var dataElements = new List<object>();

            // 3a: Storage  
            var storageIndex = BuildStorageIndex(
                storageIndexGuid, serialNumber,
                storageManifestGuid,
                cellManifestGuid,
                revisionManifestGuid,
                currentRevisionGuid);
            dataElements.Add(storageIndex);

            // 3b: Storage Manifest
            var storageManifest = BuildStorageManifest(
                storageManifestGuid, serialNumber, cellManifestGuid);
            dataElements.Add(storageManifest);

            // 3c: Cell Manifest
            var cellManifest = BuildCellManifest(
                cellManifestGuid, serialNumber, currentRevisionGuid);
            dataElements.Add(cellManifest);

            // 3d: Build chunk object graphs from ZIP analysis
            var chunkObjectGroupDataElements = new List<ObjectGroupDataElements>();
            var rootChildObjectGuids = new List<ExtendedGUID>();
            foreach (var chunk in chunks)
            {
                var objectGroupsForChunk = BuildChunkObjectGroups(
                    storageGuid,
                    serialNumber,
                    chunk,
                    ref guidCounter,
                    ref uniqueSignatureCounter,
                    out var topObjectGuidForChunk);

                rootChildObjectGuids.Add(topObjectGuidForChunk);
                chunkObjectGroupDataElements.AddRange(objectGroupsForChunk);
            }

            // 3e: Revision Manifest (references root object group + all chunk object groups)
            var allObjGroupGuids = new List<ExtendedGUID> { rootObjectGroupGuid };
            allObjGroupGuids.AddRange(chunkObjectGroupDataElements.Select(x => x.DataElementExtendedGUID));
            var revisionManifest = BuildRevisionManifest(
                revisionManifestGuid, serialNumber,
                currentRevisionGuid, baseRevisionGuid,
                rootObjectGuid, allObjGroupGuids);
            dataElements.Add(revisionManifest);

            // 3f: Root Object Group (intermediate node referencing chunk top-level nodes)
            var rootObjGroup = BuildRootObjectGroup(
                rootObjectGroupGuid, serialNumber,
                rootObjectGuid, rootChildObjectGuids, fileBytes);
            dataElements.Add(rootObjGroup);

            // 3g: Chunk object groups
            foreach (var og in chunkObjectGroupDataElements)
            {
                dataElements.Add(og);
            }

            dataElements.Reverse(); // Reverse to ensure manifests come after all referenced object groups

            // Step 4: Build the Data Element Package
            var dataElementPackage = new DataElementPackage
            {
                DataElementPackageStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElementPackage, 1, 1),
                Reserved = 0x00,
                DataElements = dataElements.ToArray(),
                DataElementPackageEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElementPackage)
            };

            // Step 5: Build Knowledge (Cell Knowledge + Waterline Knowledge + Content Tag Knowledge)
            ulong serialVal = 1;
            var knowledge = BuildKnowledge(storageGuid, serialVal);

            // Step 6: Build QueryChanges SubResponse
            var queryChangesResp = new QueryChangesResponse
            {
                queryChangesResponse = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.QueryChangesResponse,
                    QueryChangesResponseLength(storageIndexGuid), 0),
                StorageIndexExtendedGUID = storageIndexGuid,
                P = 0,
                Reserved = 0,
                Knowledge = knowledge
            };

            // Step 7: Build SubResponse wrapper
            var subResponse = new FsshttpbSubResponse
            {
                SubResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.FsshttpbSubResponse,
                    SubResponseLength(1, 2), 1),
                RequestID = FSSHTTPBSerializer.CreateCompactUint64(1),
                RequestType = FSSHTTPBSerializer.CreateCompactUint64(2), // QueryChanges = 0x02
                Status = 0,
                Reserved = 0,
                SubResponseData = queryChangesResp,
                SubResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.SubResponse)
            };

            // Step 8: Build FsshttpbResponse
            var response = new FsshttpbResponse
            {
                ProtocolVersion = 13,
                MinimumVersion = 11,
                Signature = 0x9B069439F329CF9D,
                ResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.FsshttpbResponse, 1, 1),
                Status = 0,
                Reserved = 0,
                DataElementPackage = dataElementPackage,
                SubResponses = new[] { subResponse },
                ResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.Response)
            };

            // Step 9: Serialize to bytes
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            response.Serialize(writer);
            writer.Flush();
            return ms.ToArray();
        }

        /// <summary>
        /// Build a minimal valid FSSHTTPB binary QueryChanges response with no data elements.
        /// Used for partitions where no document content is required (metadata, editors table).
        /// Returns a structurally complete FSSHTTPB response that Word accepts.
        /// </summary>
        public static byte[] BuildEmptyQueryChangesResponse()
        {
            // Step 1: Build empty Knowledge (no SpecializedKnowledge elements)
            var knowledge = new Knowledge
            {
                KnowledgeStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.Knowledge, 0, 1),
                SpecializedKnowledge = new MSFSSHTTP.Parsers.SpecializedKnowledge[0],
                KnowledgeEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.Knowledge)
            };

            // Step 2: Build QueryChanges Response with null StorageIndexExtendedGUID
            var nullStorageGuid = new ExtendedGUIDNullValue { Type = 0x00 };
            var queryChangesResp = new QueryChangesResponse
            {
                queryChangesResponse = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.QueryChangesResponse,
                    QueryChangesResponseLength(nullStorageGuid), 0),
                StorageIndexExtendedGUID = nullStorageGuid,
                P = 0,
                Reserved = 0,
                Knowledge = knowledge
            };

            // Step 3: Build SubResponse wrapper
            var subResponse = new FsshttpbSubResponse
            {
                SubResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.FsshttpbSubResponse,
                    SubResponseLength(1, 2), 1),
                RequestID = FSSHTTPBSerializer.CreateCompactUint64(1),
                RequestType = FSSHTTPBSerializer.CreateCompactUint64(2), // QueryChanges = 0x02
                Status = 0,
                Reserved = 0,
                SubResponseData = queryChangesResp,
                SubResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.SubResponse)
            };

            // Step 4: Build empty DataElementPackage (no data elements)
            var dataElementPackage = new DataElementPackage
            {
                DataElementPackageStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElementPackage, 1, 1),
                Reserved = 0x00,
                DataElements = new object[0],
                DataElementPackageEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElementPackage)
            };

            // Step 5: Build FsshttpbResponse
            var response = new FsshttpbResponse
            {
                ProtocolVersion = 13,
                MinimumVersion = 11,
                Signature = 0x9B069439F329CF9D,
                ResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.FsshttpbResponse, 1, 1),
                Status = 0,
                Reserved = 0,
                DataElementPackage = dataElementPackage,
                SubResponses = new[] { subResponse },
                ResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.Response)
            };

            // Step 6: Serialize to bytes
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            response.Serialize(writer);
            writer.Flush();
            return ms.ToArray();
        }

        /// <summary>
        /// Build a QueryAccess FSSHTTPB binary response granting both read and write access (HRESULT S_OK).
        /// Used for Cell SubRequests with no PartitionID and no DependsOn (RequestType=1).
        /// The binary is base64-encoded and placed inline as SubResponseData text content.
        /// </summary>
        public static byte[] BuildQueryAccessResponse()
        {
            // Helper: S_OK ResponseError with HRESULT GUID
            ResponseError BuildSOKResponseError() => new ResponseError
            {
                ErrorStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ResponseError, 16, compound: 1),
                ErrorTypeGUID = new Guid("8454c8f2-e401-405a-a198-a10b6991b56e"),
                ErrorData = new HRESULTError
                {
                    ErrorHRESULT = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                        StreamObjectTypeHeaderStart.HRESULTError, 4, compound: 0),
                    ErrorCode = 0  // S_OK
                },
                ErrorEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.Error)
            };

            // Step 1: Build QueryAccessResponse (read + write access both granted)
            var queryAccessResp = new QueryAccessResponse
            {
                ReadAccessResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ReadAccessResponse, 0, compound: 1),
                ReadAccessResponseError = BuildSOKResponseError(),
                ReadAccessResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.ReadAccessResponse),
                WriteAccessResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.WriteAccessResponse, 0, compound: 1),
                WriteAccessResponseError = BuildSOKResponseError(),
                WriteAccessResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.WriteAccessResponse)
            };

            // Step 2: Build SubResponse with RequestType=1 (QueryAccess)
            var subResponse = new FsshttpbSubResponse
            {
                SubResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.FsshttpbSubResponse,
                    SubResponseLength(1, 1), compound: 1),
                RequestID = FSSHTTPBSerializer.CreateCompactUint64(1),
                RequestType = FSSHTTPBSerializer.CreateCompactUint64(1), // QueryAccess = 0x01
                Status = 0,
                Reserved = 0,
                SubResponseData = queryAccessResp,
                SubResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.SubResponse)
            };

            // Step 3: Build empty DataElementPackage
            var dataElementPackage = new DataElementPackage
            {
                DataElementPackageStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElementPackage, 1, 1),
                Reserved = 0x00,
                DataElements = new object[0],
                DataElementPackageEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElementPackage)
            };

            // Step 4: Build FsshttpbResponse (ProtocolVersion=13 per spec for QueryAccess)
            var response = new FsshttpbResponse
            {
                ProtocolVersion = 13,
                MinimumVersion = 11,
                Signature = 0x9B069439F329CF9D,
                ResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.FsshttpbResponse, 1, 1),
                Status = 0,
                Reserved = 0,
                DataElementPackage = dataElementPackage,
                SubResponses = new[] { subResponse },
                ResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.Response)
            };

            // Step 5: Serialize to bytes
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            response.Serialize(writer);
            writer.Flush();
            return ms.ToArray();
        }


        public static byte[] BuildMinimalPartitionResponse(string webRootPath = "", ulong requestId = 1)
        {
            var sessionGuid = Guid.NewGuid();
            var replicaId = Guid.NewGuid();
            var indexMappingId = Guid.NewGuid();

            // All ExGuids use the same session GUID with incrementing values
            var storageIndexGuid = CreateExGuid32Bit(sessionGuid, 1);
            var storageManifestGuid = CreateExGuid32Bit(sessionGuid, 2);
            var cellManifestGuid = CreateExGuid32Bit(sessionGuid, 3);
            var revisionManifestGuid = CreateExGuid32Bit(sessionGuid, 4);
            var revisionIdGuid = CreateExGuid32Bit(sessionGuid, 5);
            var rootObjectGuid = CreateExGuid32Bit(sessionGuid, 6);
            var objectGroupGuid = CreateExGuid32Bit(sessionGuid, 7);
            var objectGroup2Guid = CreateExGuid32Bit(sessionGuid, 8);
            var leafObjectGuid = CreateExGuid32Bit(sessionGuid, 9);
            var dataNodeGuid = CreateExGuid32Bit(sessionGuid, 10);

            var dataNodeGuidExtendedArray = new ExtendedGUIDArray
            {
                Count = FSSHTTPBSerializer.CreateCompactUint64(1),
                Content = new[] { dataNodeGuid }
            };

            var leafNodeGuidExtendedArray = new ExtendedGUIDArray
            {
                Count = FSSHTTPBSerializer.CreateCompactUint64(1),
                Content = new[] { leafObjectGuid }
            };

            // Single serial number for all data elements — serial 1
            ulong snVal = 1;
            var snRevisionManifest = new SerialNumber64BitUintValue { Type = 0x80, GUID = replicaId, Value = snVal++ };
            var snStorageManifest = new SerialNumber64BitUintValue { Type = 0x80, GUID = replicaId, Value = snVal++ };
            var snObjectGroup1 = new SerialNumber64BitUintValue { Type = 0x80, GUID = replicaId, Value = snVal++ };
            var snCellManifest = new SerialNumber64BitUintValue { Type = 0x80, GUID = replicaId, Value = snVal++ };
            var snObjectGroup2 = new SerialNumber64BitUintValue { Type = 0x80, GUID = replicaId, Value = snVal++ };

            // Storage Index uses sessionGuid instead of replicaId
            var snStorageIndex = new SerialNumber64BitUintValue { Type = 0x80, GUID = sessionGuid, Value = snVal++ };

            // Well-known Cell ID (same as file contents partition per MS-FSSHTTPD)
            var cellId = new CellID
            {
                EXGUID1 = new ExtendedGUID5BitUintValue
                { Type = 0x04, Value = 1, GUID = CellGuid1 },
                EXGUID2 = new ExtendedGUID5BitUintValue
                { Type = 0x04, Value = 1, GUID = CellGuid2 }
            };

            // Build a leaf node (empty data) that the root IntermediateNode will reference.
            // Per MS-FSSHTTPD 2.2.3: LeafNodeObject = SignatureObject + DataSizeObject.
            byte[] leafNodeDataBytes = BuildEditorsTablePayloadFromFile(webRootPath);
            var leafNodeDataSize = (ulong)leafNodeDataBytes.Length;

            byte[] leafNodeSignature;
            using (var md5 = MD5.Create())
            {
                // Compute the hash of the raw bytes going into the Data Node
                leafNodeSignature = md5.ComputeHash(leafNodeDataBytes);
            }

            byte[] leafNodeBytes = BuildLeafNodeBytes(leafNodeSignature, leafNodeDataSize);


            // Minimal IntermediateNode — empty signature, DataSize=0, references one child leaf
            byte[] rootNodeBytes = BuildIntermediateNodeBytes(Array.Empty<byte>(), leafNodeDataSize); // leafNodeSize - sum of all such leaf node sizes - intermediate node data sizes

            var dataElements = new object[]
            {

        // 1. Revision Manifest
        new RevisionManifestDataElement
        {
            DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.DataElement,
                DataElementStartLength(revisionManifestGuid, snRevisionManifest, (ulong)DataElementTypes.RevisionManifest), 1),
            DataElementExtendedGUID = revisionManifestGuid,
            SerialNumber = snRevisionManifest,
            DataElementType = FSSHTTPBSerializer.CreateCompactUint64(
                (ulong)DataElementTypes.RevisionManifest),
            RevisionManifest = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.RevisionManifest,
                RevisionManifestLength(revisionIdGuid, new ExtendedGUIDNullValue { Type = 0x00 }), 1),
            RevisionID = revisionIdGuid,
            BaseRevisionID = new ExtendedGUIDNullValue { Type = 0x00 },
            RevisionManifestDataElementsData = new object[]
            {
                new RevisionManifestRootDeclareValues
                {
                    RevisionManifestRootDeclare = FSSHTTPBSerializer
                        .Create16BitStreamObjectHeaderStart(
                            StreamObjectTypeHeaderStart.RevisionManifestRootDeclare,
                            RevisionManifestRootDeclareLength(
                                new ExtendedGUID5BitUintValue { Type = 0x04, Value = 2, GUID = RootGuid },
                                rootObjectGuid)),
                    RootExtendedGUID = new ExtendedGUID5BitUintValue
                        { Type = 0x04, Value = 2, GUID = RootGuid },
                    ObjectExtendedGUID = rootObjectGuid
                },
                new RevisionManifestObjectGroupReferencesValues
                {
                    RevisionManifestObjectGroupReferences = FSSHTTPBSerializer
                        .Create16BitStreamObjectHeaderStart(
                            StreamObjectTypeHeaderStart.RevisionManifestObjectGroupReferences,
                            RevisionManifestObjectGroupReferencesLength(objectGroupGuid)),
                    ObjectGroupExtendedGUID = objectGroupGuid
                },
                new RevisionManifestObjectGroupReferencesValues
                {
                    RevisionManifestObjectGroupReferences = FSSHTTPBSerializer
                        .Create16BitStreamObjectHeaderStart(
                            StreamObjectTypeHeaderStart.RevisionManifestObjectGroupReferences,
                            RevisionManifestObjectGroupReferencesLength(objectGroup2Guid)),
                    ObjectGroupExtendedGUID = objectGroup2Guid
                }
            },
            DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                StreamObjectTypeHeaderEnd.DataElement)
        },

        // 2. Storage Manifest
        new StorageManifestDataElement
        {
            DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.DataElement,
                DataElementStartLength(storageManifestGuid, snStorageManifest, (ulong)DataElementTypes.StorageManifest), 1),
            DataElementExtendedGUID = storageManifestGuid,
            SerialNumber = snStorageManifest,
            DataElementType = FSSHTTPBSerializer.CreateCompactUint64(
                (ulong)DataElementTypes.StorageManifest),
            StorageManifestSchemaGUID = FSSHTTPBSerializer
                .Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.StorageManifestSchemaGUID, 16),
            GUID = StorageManifestSchemaGuid,
            StorageManifestRootDeclare = new[]
            {
                new StorageManifestRootDeclareValues
                {
                    StorageManifestRootDeclare = FSSHTTPBSerializer
                        .Create16BitStreamObjectHeaderStart(
                            StreamObjectTypeHeaderStart.StorageManifestRootDeclare,
                            StorageManifestRootDeclareLength(
                                new ExtendedGUID5BitUintValue { Type = 0x04, Value = 2, GUID = RootGuid },
                                cellId)),
                    RootExtendedGUID = new ExtendedGUID5BitUintValue
                        { Type = 0x04, Value = 2, GUID = RootGuid },
                    CellID = cellId
                }
            },
            DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                StreamObjectTypeHeaderEnd.DataElement)
        },
        
        
        // 3. Object Group — single root IntermediateNode, no data children
        new ObjectGroupDataElements
        {
            DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.DataElement,
                DataElementStartLength(objectGroupGuid, snObjectGroup1, (ulong)DataElementTypes.ObjectGroup), 1),
            DataElementExtendedGUID = objectGroupGuid,
            SerialNumber = snObjectGroup1,
            DataElementType = FSSHTTPBSerializer.CreateCompactUint64((ulong)DataElementTypes.ObjectGroup),
            ObjectGroupDeclarationsStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.ObjectGroupDeclarations, 0, 1),
            ObjectDeclarationOrObjectDataBLOBDeclaration = new object[]
            {
                new ObjectDeclaration
                {
                    ObjectGroupObjectDeclaration = FSSHTTPBSerializer
                        .Create16BitStreamObjectHeaderStart(
                            StreamObjectTypeHeaderStart.ObjectGroupObjectDeclare,
                            ObjectDeclareLength(rootObjectGuid, 1, 25, 0, 0)),
                    ObjectExtendedGUID = rootObjectGuid, // 21 bytes
                    ObjectPartitionID = FSSHTTPBSerializer.CreateCompactUint64(1), // 1 byte
                    ObjectDataSize = FSSHTTPBSerializer.CreateCompactUint64(
                        (ulong)rootNodeBytes.Length), // 1 byte
                    ObjectReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(1), // 1 byte
                    CellReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(0) // 1 byte 
                }
            },
            ObjectGroupDeclarationsEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                StreamObjectTypeHeaderEnd.ObjectGroupDeclarations),
            ObjectMetadataDeclaration =  new ObjectMetadataDeclaration()
            {
                ObjectGroupMetadataDeclarations = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(StreamObjectTypeHeaderStart.ObjectGroupMetadataDeclarations, 0, 1),
                ObjectMetadata = new ObjectMetadata[]
                {
                    new ObjectMetadata()
                    {
                        ObjectGroupMetadata = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(StreamObjectTypeHeaderStart.ObjectGroupMetadata, 1, 0),
                        ObjectChangeFrequency = FSSHTTPBSerializer.CreateCompactUint64(3)
                    }
                },
                ObjectGroupMetadataDeclarationsEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(StreamObjectTypeHeaderEnd.ObjectGroupMetadataDeclarations)
            },
            ObjectGroupDataStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.ObjectGroupData, 0, 1),
            ObjectDataOrObjectDataBLOBReference = new object[]
            {
                new ObjectData
                {
                    ObjectGroupObjectDataOrExcludedData = CreateObjectDataHeader(leafNodeGuidExtendedArray.Content, (ulong)rootNodeBytes.Length),
                    ObjectExtendedGUIDArray = leafNodeGuidExtendedArray, // 22 bytes
                    CellIDArray = new CellIDArray
                        { Count = FSSHTTPBSerializer.CreateCompactUint64(0) }, // 1 byte
                    DataSize = FSSHTTPBSerializer.CreateCompactUint64(
                        (ulong)rootNodeBytes.Length), // 1 byte
                    Data = rootNodeBytes
                }
            },
            ObjectGroupDataEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                StreamObjectTypeHeaderEnd.ObjectGroupData),
            DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                StreamObjectTypeHeaderEnd.DataElement)
        },
        
        // 4. Cell Manifest
        new CellManifestDataElement
        {
            DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.DataElement,
                DataElementStartLength(cellManifestGuid, snCellManifest, (ulong)DataElementTypes.CellManifest), 1),
            DataElementExtendedGUID = cellManifestGuid,
            SerialNumber = snCellManifest,
            DataElementType = FSSHTTPBSerializer.CreateCompactUint64(
                (ulong)DataElementTypes.CellManifest),
            CellManifestCurrentRevision = FSSHTTPBSerializer
                .Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.CellManifestCurrentRevision,
                    CellManifestCurrentRevisionLength(revisionIdGuid)),
            CellManifestCurrentRevisionExtendedGUID = revisionIdGuid,
            DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                StreamObjectTypeHeaderEnd.DataElement)
        },

        // 5. leaf node objects
        new ObjectGroupDataElements
        {
            DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.DataElement,
                DataElementStartLength(objectGroup2Guid, snObjectGroup2, (ulong)DataElementTypes.ObjectGroup), 1),
            DataElementExtendedGUID = objectGroup2Guid,
            SerialNumber = snObjectGroup2,
            DataElementType = FSSHTTPBSerializer.CreateCompactUint64((ulong)DataElementTypes.ObjectGroup),
            ObjectGroupDeclarationsStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.ObjectGroupDeclarations, 0, 1),
            ObjectDeclarationOrObjectDataBLOBDeclaration = new object[]
            {
                new ObjectDeclaration
                {
                    ObjectGroupObjectDeclaration = FSSHTTPBSerializer
                        .Create16BitStreamObjectHeaderStart(
                            StreamObjectTypeHeaderStart.ObjectGroupObjectDeclare,
                            ObjectDeclareLength(leafObjectGuid, 1, (ulong)leafNodeBytes.Length, 1, 0)),
                    ObjectExtendedGUID = leafObjectGuid, // 21 bytes
                    ObjectPartitionID = FSSHTTPBSerializer.CreateCompactUint64(1), // 1 byte
                    ObjectDataSize = FSSHTTPBSerializer.CreateCompactUint64(
                        (ulong)leafNodeBytes.Length),
                    ObjectReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(1), // 1 byte
                    CellReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(0) // 1 byte 
                },
                new ObjectDeclaration
                {
                    ObjectGroupObjectDeclaration = FSSHTTPBSerializer
                        .Create16BitStreamObjectHeaderStart(
                            StreamObjectTypeHeaderStart.ObjectGroupObjectDeclare,
                            ObjectDeclareLength(dataNodeGuid, 1, leafNodeDataSize, 0, 0)),
                    ObjectExtendedGUID = dataNodeGuid, // 21 bytes
                    ObjectPartitionID = FSSHTTPBSerializer.CreateCompactUint64(1), // 1 byte
                    ObjectDataSize = FSSHTTPBSerializer.CreateCompactUint64(
                        leafNodeDataSize),
                    ObjectReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(0), // 1 byte
                    CellReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(0) // 1 byte 
                }
            },
            ObjectGroupDeclarationsEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                StreamObjectTypeHeaderEnd.ObjectGroupDeclarations),
            ObjectMetadataDeclaration =  new ObjectMetadataDeclaration()
            {
                ObjectGroupMetadataDeclarations = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(StreamObjectTypeHeaderStart.ObjectGroupMetadataDeclarations, 0, 1),
                ObjectMetadata = new ObjectMetadata[]
                {
                    new ObjectMetadata()
                    {
                        ObjectGroupMetadata = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(StreamObjectTypeHeaderStart.ObjectGroupMetadata, 1, 0),
                        ObjectChangeFrequency = FSSHTTPBSerializer.CreateCompactUint64(4)
                    },
                    new ObjectMetadata()
                    {
                        ObjectGroupMetadata = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(StreamObjectTypeHeaderStart.ObjectGroupMetadata, 1, 0),
                        ObjectChangeFrequency = FSSHTTPBSerializer.CreateCompactUint64(4)
                    },
                },
                ObjectGroupMetadataDeclarationsEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(StreamObjectTypeHeaderEnd.ObjectGroupMetadataDeclarations)
            },
            ObjectGroupDataStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.ObjectGroupData, 0, 1),
            ObjectDataOrObjectDataBLOBReference = new object[]
            {
                new ObjectData
                {
                    ObjectGroupObjectDataOrExcludedData = CreateObjectDataHeader(dataNodeGuidExtendedArray.Content, (ulong)leafNodeBytes.Length),
                    ObjectExtendedGUIDArray = dataNodeGuidExtendedArray,
                    CellIDArray = new CellIDArray
                        { Count = FSSHTTPBSerializer.CreateCompactUint64(0) },
                    DataSize = FSSHTTPBSerializer.CreateCompactUint64(
                        (ulong)leafNodeBytes.Length),
                    Data = leafNodeBytes
                },
                new ObjectData
                {
                    ObjectGroupObjectDataOrExcludedData = CreateObjectDataHeader(null, leafNodeDataSize),
                    ObjectExtendedGUIDArray = new ExtendedGUIDArray
                    {
                        Count = FSSHTTPBSerializer.CreateCompactUint64(0)
                    },
                    CellIDArray = new CellIDArray
                        { Count = FSSHTTPBSerializer.CreateCompactUint64(0) },
                    DataSize = FSSHTTPBSerializer.CreateCompactUint64(
                        leafNodeDataSize),
                    Data = leafNodeDataBytes
                },
            },

            ObjectGroupDataEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                StreamObjectTypeHeaderEnd.ObjectGroupData),
            DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                StreamObjectTypeHeaderEnd.DataElement)
        },
        
        // 6. Storage Index
        new StorageIndexDataElement
        {
            DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.DataElement,
                DataElementStartLength(storageIndexGuid, snStorageIndex, (ulong)DataElementTypes.StorageIndex), 1),
            DataElementExtendedGUID = storageIndexGuid,
            SerialNumber = snStorageIndex,
            DataElementType = FSSHTTPBSerializer.CreateCompactUint64(
                (ulong)DataElementTypes.StorageIndex),
            StorageIndexDataElementData = new object[]
            {
                new StorageIndexCellMappingValues
                {
                    StorageIndexCellMapping = FSSHTTPBSerializer
                        .Create16BitStreamObjectHeaderStart(
                            StreamObjectTypeHeaderStart.StorageIndexCellMapping,
                            StorageIndexCellMappingLength(cellId, cellManifestGuid, snCellManifest)),
                    CellID = cellId,
                    CellMappingExtendedGUID = cellManifestGuid,
                    CellMappingSerialNumber = snCellManifest
                },
                new StorageIndexManifestMappingValues
                {
                    StorageIndexManifestMapping = FSSHTTPBSerializer
                        .Create16BitStreamObjectHeaderStart(
                            StreamObjectTypeHeaderStart.StorageIndexManifestMapping,
                            StorageIndexManifestMappingLength(storageManifestGuid, snStorageManifest)),
                    ManifestMappingExtendedGUID = storageManifestGuid,
                    ManifestMappingSerialNumber = snStorageManifest
                },
                new StorageIndexRevisionMappingValues
                {
                    StorageIndexRevisionMapping = FSSHTTPBSerializer
                        .Create16BitStreamObjectHeaderStart(
                            StreamObjectTypeHeaderStart.StorageIndexRevisionMapping,
                            StorageIndexRevisionMappingLength(revisionIdGuid, revisionManifestGuid, snRevisionManifest)),
                    RevisionExtendedGUID = revisionIdGuid,
                    RevisionMappingExtendedGUID = revisionManifestGuid,
                    RevisionMappingSerialNumber = snRevisionManifest
                }
            },
            DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                StreamObjectTypeHeaderEnd.DataElement)
        },


            };

            // Knowledge with Cell + Waterline, serial 0→1
            var knowledge = BuildEmptyKnowledge();

            var queryChangesResp = new QueryChangesResponse
            {
                queryChangesResponse = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.QueryChangesResponse, QueryChangesResponseLength(storageIndexGuid), 0),
                StorageIndexExtendedGUID = storageIndexGuid,   // ← non-null 21byte
                P = 0,
                Reserved = 0,
                Knowledge = knowledge
            };

            var subResponse = new FsshttpbSubResponse
            {
                SubResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.FsshttpbSubResponse, SubResponseLength(requestId, 2), 1),
                RequestID = FSSHTTPBSerializer.CreateCompactUint64(requestId),
                RequestType = FSSHTTPBSerializer.CreateCompactUint64(2),
                Status = 0,
                Reserved = 0,
                SubResponseData = queryChangesResp,
                SubResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.SubResponse)
            };

            var dataElementPackage = new DataElementPackage
            {
                DataElementPackageStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElementPackage, 1, 1),
                Reserved = 0x00,
                DataElements = dataElements,
                DataElementPackageEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElementPackage)
            };

            var response = new FsshttpbResponse
            {
                ProtocolVersion = 13,
                MinimumVersion = 11,
                Signature = 0x9B069439F329CF9D,
                ResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.FsshttpbResponse, 1, 1),
                Status = 0,
                Reserved = 0,
                DataElementPackage = dataElementPackage,
                SubResponses = new[] { subResponse },
                ResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.Response)
            };

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            response.Serialize(writer);
            writer.Flush();
            return ms.ToArray();
        }

        /// <summary>
        /// Build a minimal FSSHTTPB QueryChanges response containing a single StorageIndex
        /// data element with only a null-mapped StorageIndexManifestMapping and empty Knowledge.
        /// Used for partitions that need a structurally valid response with no actual content.
        /// </summary>
        public static byte[] BuildEmptyStorageIndexResponse(ulong requestId = 1)
        {
            var sessionGuid = Guid.NewGuid();
            var replicaId = Guid.NewGuid();

            var storageIndexGuid = CreateExGuid32Bit(sessionGuid, 1);
            var snStorageIndex = new SerialNumber64BitUintValue { Type = 0x80, GUID = sessionGuid, Value = 2 };

            // StorageIndex with a single StorageIndexManifestMapping using null GUID and null serial number
            var nullManifestGuid = new ExtendedGUIDNullValue { Type = 0x00 };
            var nullSerialNumber = new SerialNumberNullValue { Type = 0x00 };

            var dataElements = new object[]
            {
                new StorageIndexDataElement
                {
                    DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                        StreamObjectTypeHeaderStart.DataElement,
                        DataElementStartLength(storageIndexGuid, snStorageIndex, (ulong)DataElementTypes.StorageIndex), 1),
                    DataElementExtendedGUID = storageIndexGuid,
                    SerialNumber = snStorageIndex,
                    DataElementType = FSSHTTPBSerializer.CreateCompactUint64(
                        (ulong)DataElementTypes.StorageIndex),
                    StorageIndexDataElementData = new object[]
                    {
                        new StorageIndexManifestMappingValues
                        {
                            StorageIndexManifestMapping = FSSHTTPBSerializer
                                .Create16BitStreamObjectHeaderStart(
                                    StreamObjectTypeHeaderStart.StorageIndexManifestMapping,
                                    StorageIndexManifestMappingLength(nullManifestGuid, nullSerialNumber)),
                            ManifestMappingExtendedGUID = nullManifestGuid,
                            ManifestMappingSerialNumber = nullSerialNumber
                        }
                    },
                    DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                        StreamObjectTypeHeaderEnd.DataElement)
                }
            };

            var knowledge = BuildEmptyKnowledge();

            var queryChangesResp = new QueryChangesResponse
            {
                queryChangesResponse = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.QueryChangesResponse,
                    QueryChangesResponseLength(storageIndexGuid), 0),
                StorageIndexExtendedGUID = storageIndexGuid,
                P = 0,
                Reserved = 0,
                Knowledge = knowledge
            };

            var subResponse = new FsshttpbSubResponse
            {
                SubResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.FsshttpbSubResponse,
                    SubResponseLength(requestId, 2), 1),
                RequestID = FSSHTTPBSerializer.CreateCompactUint64(requestId),
                RequestType = FSSHTTPBSerializer.CreateCompactUint64(2),
                Status = 0,
                Reserved = 0,
                SubResponseData = queryChangesResp,
                SubResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.SubResponse)
            };

            var dataElementPackage = new DataElementPackage
            {
                DataElementPackageStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElementPackage, 1, 1),
                Reserved = 0x00,
                DataElements = dataElements,
                DataElementPackageEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElementPackage)
            };

            var response = new FsshttpbResponse
            {
                ProtocolVersion = 13,
                MinimumVersion = 11,
                Signature = 0x9B069439F329CF9D,
                ResponseStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.FsshttpbResponse, 1, 1),
                Status = 0,
                Reserved = 0,
                DataElementPackage = dataElementPackage,
                SubResponses = new[] { subResponse },
                ResponseEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.Response)
            };

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            response.Serialize(writer);
            writer.Flush();
            return ms.ToArray();
        }

        private static ExtendedGUID32BitUintValue CreateExGuid32Bit(Guid guid, uint value)
            => new ExtendedGUID32BitUintValue { Type = 0x80, Value = value, GUID = guid };


        #region File Chunking (MS-FSSHTTPD Section 2.4.1 - ZIP Based Chunking)

        private sealed class AnalyzedChunk
        {
            public byte[] Bytes { get; set; }
            public byte[] Signature { get; set; }
        }

        private sealed class LocalFileHeaderInfo
        {
            public int HeaderLength { get; set; }
            public int DataStart { get; set; }
            public long DataEnd { get; set; }
            public uint Crc32 { get; set; }
            public ulong CompressedSize { get; set; }
            public ulong UncompressedSize { get; set; }
        }

        /// <summary>
        /// Implements MS-FSSHTTPD 2.4.1 ZIP analysis from raw file bytes.
        /// </summary>
        private static List<AnalyzedChunk> ChunkFile(byte[] fileBytes)
        {
            var chunks = new List<AnalyzedChunk>();
            var offset = 0;

            // If local file header signature is not present at file start, ZIP analysis is not used.
            if (!IsLocalFileHeaderSignature(fileBytes, 0))
            {
                return CreateFallbackChunks(fileBytes);
            }

            while (offset < fileBytes.Length)
            {
                if (!IsLocalFileHeaderSignature(fileBytes, offset))
                {
                    break;
                }

                if (!TryReadLocalHeader(fileBytes, offset, out var header))
                {
                    break;
                }

                if (header.DataEnd > fileBytes.Length)
                {
                    break;
                }

                var localHeaderChunk = SliceBytes(fileBytes, offset, header.HeaderLength);
                var dataChunk = SliceBytes(fileBytes, header.DataStart, (int)(header.DataEnd - header.DataStart));

                var localHeaderSignature = Sha1(localHeaderChunk);
                var dataChunkSignature = BuildDataChunkSignature(header.Crc32, header.CompressedSize, header.UncompressedSize);

                if ((localHeaderChunk.Length + dataChunk.Length) <= 4096)
                {
                    // For protocol versions >= 2.2 this is signature XOR, then append any extra bytes from longer signature.
                    var combinedChunk = Concatenate(localHeaderChunk, dataChunk);
                    chunks.Add(new AnalyzedChunk
                    {
                        Bytes = combinedChunk,
                        Signature = CombineSignatures(localHeaderSignature, dataChunkSignature)
                    });
                }
                else
                {
                    chunks.Add(new AnalyzedChunk { Bytes = localHeaderChunk, Signature = localHeaderSignature });
                    chunks.Add(new AnalyzedChunk { Bytes = dataChunk, Signature = dataChunkSignature });
                }

                offset = (int)header.DataEnd;
            }

            // If no chunks were produced, ZIP analysis MUST NOT be used.
            if (chunks.Count == 0)
            {
                return CreateFallbackChunks(fileBytes);
            }

            // Remaining bytes become final chunk.
            if (offset < fileBytes.Length)
            {
                var remaining = SliceBytes(fileBytes, offset, fileBytes.Length - offset);
                var finalSignature = remaining.Length <= 1048576
                    ? Sha1(remaining)
                    : BuildLargeFinalChunkSignature(remaining);

                chunks.Add(new AnalyzedChunk
                {
                    Bytes = remaining,
                    Signature = finalSignature
                });
            }

            return chunks;
        }

        private static bool IsLocalFileHeaderSignature(byte[] bytes, int offset)
        {
            return offset + 4 <= bytes.Length
                && bytes[offset] == 0x50
                && bytes[offset + 1] == 0x4B
                && bytes[offset + 2] == 0x03
                && bytes[offset + 3] == 0x04;
        }

        private static bool TryReadLocalHeader(byte[] bytes, int offset, out LocalFileHeaderInfo header)
        {
            header = null;
            if (offset + 30 > bytes.Length)
            {
                return false;
            }

            ushort generalPurposeBitFlag = BitConverter.ToUInt16(bytes, offset + 6);
            uint crc32 = BitConverter.ToUInt32(bytes, offset + 14);
            uint compressedSize32 = BitConverter.ToUInt32(bytes, offset + 18);
            uint uncompressedSize32 = BitConverter.ToUInt32(bytes, offset + 22);
            ushort fileNameLength = BitConverter.ToUInt16(bytes, offset + 26);
            ushort extraFieldLength = BitConverter.ToUInt16(bytes, offset + 28);

            int headerLength = 30 + fileNameLength + extraFieldLength;
            if (offset + headerLength > bytes.Length)
            {
                return false;
            }

            ulong compressedSize = compressedSize32;
            ulong uncompressedSize = uncompressedSize32;

            if (compressedSize32 == uint.MaxValue || uncompressedSize32 == uint.MaxValue)
            {
                ReadZip64Sizes(bytes, offset + 30 + fileNameLength, extraFieldLength, compressedSize32, uncompressedSize32, ref compressedSize, ref uncompressedSize);
            }

            // If bit 3 is set and size is unknown in the local header, this implementation stops ZIP analysis.
            if ((generalPurposeBitFlag & 0x0008) != 0 && compressedSize == 0)
            {
                return false;
            }

            int dataStart = offset + headerLength;
            long dataEnd = dataStart + (long)compressedSize;

            if (dataEnd < dataStart)
            {
                return false;
            }

            header = new LocalFileHeaderInfo
            {
                HeaderLength = headerLength,
                DataStart = dataStart,
                DataEnd = dataEnd,
                Crc32 = crc32,
                CompressedSize = compressedSize,
                UncompressedSize = uncompressedSize
            };

            return true;
        }

        private static void ReadZip64Sizes(
            byte[] bytes,
            int extraOffset,
            int extraLength,
            uint compressedSize32,
            uint uncompressedSize32,
            ref ulong compressedSize,
            ref ulong uncompressedSize)
        {
            int cursor = extraOffset;
            int end = extraOffset + extraLength;

            while (cursor + 4 <= end)
            {
                ushort headerId = BitConverter.ToUInt16(bytes, cursor);
                ushort dataSize = BitConverter.ToUInt16(bytes, cursor + 2);
                cursor += 4;

                if (cursor + dataSize > end)
                {
                    break;
                }

                if (headerId == 0x0001)
                {
                    int p = cursor;
                    int zip64End = cursor + dataSize;

                    if (uncompressedSize32 == uint.MaxValue && p + 8 <= zip64End)
                    {
                        uncompressedSize = BitConverter.ToUInt64(bytes, p);
                        p += 8;
                    }

                    if (compressedSize32 == uint.MaxValue && p + 8 <= zip64End)
                    {
                        compressedSize = BitConverter.ToUInt64(bytes, p);
                    }

                    return;
                }

                cursor += dataSize;
            }
        }

        private static byte[] BuildDataChunkSignature(uint crc32, ulong compressedSize, ulong uncompressedSize)
        {
            var signature = new byte[20];
            Array.Copy(BitConverter.GetBytes(crc32), 0, signature, 0, 4);
            Array.Copy(BitConverter.GetBytes(compressedSize), 0, signature, 4, 8);
            Array.Copy(BitConverter.GetBytes(uncompressedSize), 0, signature, 12, 8);
            return signature;
        }

        private static byte[] CombineSignatures(byte[] left, byte[] right)
        {
            int min = Math.Min(left.Length, right.Length);
            int max = Math.Max(left.Length, right.Length);
            var output = new byte[max];

            for (int i = 0; i < min; i++)
            {
                output[i] = (byte)(left[i] ^ right[i]);
            }

            if (left.Length > right.Length)
            {
                Array.Copy(left, min, output, min, left.Length - min);
            }
            else if (right.Length > left.Length)
            {
                Array.Copy(right, min, output, min, right.Length - min);
            }

            return output;
        }

        private static byte[] BuildLargeFinalChunkSignature(byte[] finalChunk)
        {
            // 12-byte unique sequence for large final chunk signature.
            // Deterministic uniqueness is achieved using SHA-1 prefix + size tail.
            byte[] sha = Sha1(finalChunk);
            byte[] sig = new byte[12];
            Array.Copy(sha, 0, sig, 0, 8);
            Array.Copy(BitConverter.GetBytes((uint)finalChunk.Length), 0, sig, 8, 4);
            return sig;
        }

        private static List<AnalyzedChunk> CreateFallbackChunks(byte[] fileBytes)
        {
            return new List<AnalyzedChunk>
            {
                new AnalyzedChunk
                {
                    Bytes = fileBytes,
                    Signature = Sha1(fileBytes)
                }
            };
        }

        private static byte[] Sha1(byte[] bytes)
        {
            using var sha1 = SHA1.Create();
            return sha1.ComputeHash(bytes);
        }

        private static byte[] SliceBytes(byte[] source, int offset, int length)
        {
            var result = new byte[length];
            Array.Copy(source, offset, result, 0, length);
            return result;
        }

        private static byte[] Concatenate(byte[] left, byte[] right)
        {
            var bytes = new byte[left.Length + right.Length];
            Buffer.BlockCopy(left, 0, bytes, 0, left.Length);
            Buffer.BlockCopy(right, 0, bytes, left.Length, right.Length);
            return bytes;
        }

        #endregion

        #region Data Element Builders

        private static StorageIndexDataElement BuildStorageIndex(
            ExtendedGUID storageIndexGuid,
            SerialNumber serialNumber,
            ExtendedGUID storageManifestGuid,
            ExtendedGUID cellManifestGuid,
            ExtendedGUID revisionManifestGuid,
            ExtendedGUID currentRevisionGuid)
        {
            // Cell ID for default partition
            var cellId = new CellID
            {
                EXGUID1 = CreateExGuid5Bit(CellGuid1, 1),
                EXGUID2 = CreateExGuid5Bit(CellGuid2, 1)
            };

            var manifestMapping = new StorageIndexManifestMappingValues
            {
                StorageIndexManifestMapping = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.StorageIndexManifestMapping,
                    StorageIndexManifestMappingLength(storageManifestGuid, serialNumber)),
                ManifestMappingExtendedGUID = storageManifestGuid,
                ManifestMappingSerialNumber = serialNumber
            };

            var cellMapping = new StorageIndexCellMappingValues
            {
                StorageIndexCellMapping = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.StorageIndexCellMapping,
                    StorageIndexCellMappingLength(cellId, cellManifestGuid, serialNumber)),
                CellID = cellId,
                CellMappingExtendedGUID = cellManifestGuid,
                CellMappingSerialNumber = serialNumber
            };

            var revisionMapping = new StorageIndexRevisionMappingValues
            {
                StorageIndexRevisionMapping = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.StorageIndexRevisionMapping,
                    StorageIndexRevisionMappingLength(currentRevisionGuid, revisionManifestGuid, serialNumber)),
                RevisionExtendedGUID = currentRevisionGuid,
                RevisionMappingExtendedGUID = revisionManifestGuid,
                RevisionMappingSerialNumber = serialNumber
            };

            return new StorageIndexDataElement
            {
                DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElement,
                    DataElementStartLength(storageIndexGuid, serialNumber, (ulong)DataElementTypes.StorageIndex), 1),
                DataElementExtendedGUID = storageIndexGuid,
                SerialNumber = serialNumber,
                DataElementType = FSSHTTPBSerializer.CreateCompactUint64((ulong)DataElementTypes.StorageIndex),
                StorageIndexDataElementData = new object[] { manifestMapping, cellMapping, revisionMapping },
                DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElement)
            };
        }

        private static StorageManifestDataElement BuildStorageManifest(
            ExtendedGUID storageManifestGuid,
            SerialNumber serialNumber,
            ExtendedGUID cellManifestGuid)
        {
            var cellId = new CellID
            {
                EXGUID1 = CreateExGuid5Bit(CellGuid1, 1),
                EXGUID2 = CreateExGuid5Bit(CellGuid2, 1)
            };

            var rootExGuid = CreateExGuid5Bit(RootGuid, 2);
            var rootDeclare = new StorageManifestRootDeclareValues
            {
                StorageManifestRootDeclare = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.StorageManifestRootDeclare,
                    StorageManifestRootDeclareLength(rootExGuid, cellId)),
                RootExtendedGUID = rootExGuid,
                CellID = cellId
            };

            return new StorageManifestDataElement
            {
                DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElement,
                    DataElementStartLength(storageManifestGuid, serialNumber, (ulong)DataElementTypes.StorageManifest), 1),
                DataElementExtendedGUID = storageManifestGuid,
                SerialNumber = serialNumber,
                DataElementType = FSSHTTPBSerializer.CreateCompactUint64((ulong)DataElementTypes.StorageManifest),
                StorageManifestSchemaGUID = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.StorageManifestSchemaGUID, 16),
                GUID = StorageManifestSchemaGuid,
                StorageManifestRootDeclare = new[] { rootDeclare },
                DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElement)
            };
        }

        private static CellManifestDataElement BuildCellManifest(
            ExtendedGUID cellManifestGuid,
            SerialNumber serialNumber,
            ExtendedGUID currentRevisionGuid)
        {
            return new CellManifestDataElement
            {
                DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElement,
                    DataElementStartLength(cellManifestGuid, serialNumber, (ulong)DataElementTypes.CellManifest), 1),
                DataElementExtendedGUID = cellManifestGuid,
                SerialNumber = serialNumber,
                DataElementType = FSSHTTPBSerializer.CreateCompactUint64((ulong)DataElementTypes.CellManifest),
                CellManifestCurrentRevision = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.CellManifestCurrentRevision,
                    CellManifestCurrentRevisionLength(currentRevisionGuid)),
                CellManifestCurrentRevisionExtendedGUID = currentRevisionGuid,
                DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElement)
            };
        }

        private static RevisionManifestDataElement BuildRevisionManifest(
            ExtendedGUID revisionManifestGuid,
            SerialNumber serialNumber,
            ExtendedGUID currentRevisionGuid,
            ExtendedGUID baseRevisionGuid,
            ExtendedGUID rootObjectGuid,
            List<ExtendedGUID> objectGroupGuids)
        {
            var subElements = new List<object>();

            // Root Declare: points root GUID to the root object
            var rootExGuid = CreateExGuid5Bit(RootGuid, 2);
            subElements.Add(new RevisionManifestRootDeclareValues
            {
                RevisionManifestRootDeclare = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.RevisionManifestRootDeclare,
                    RevisionManifestRootDeclareLength(rootExGuid, rootObjectGuid)),
                RootExtendedGUID = rootExGuid,
                ObjectExtendedGUID = rootObjectGuid
            });

            // Object Group References: one per object group
            foreach (var ogGuid in objectGroupGuids)
            {
                subElements.Add(new RevisionManifestObjectGroupReferencesValues
                {
                    RevisionManifestObjectGroupReferences = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                        StreamObjectTypeHeaderStart.RevisionManifestObjectGroupReferences,
                        RevisionManifestObjectGroupReferencesLength(ogGuid)),
                    ObjectGroupExtendedGUID = ogGuid
                });
            }

            return new RevisionManifestDataElement
            {
                DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElement,
                    DataElementStartLength(revisionManifestGuid, serialNumber, (ulong)DataElementTypes.RevisionManifest), 1),
                DataElementExtendedGUID = revisionManifestGuid,
                SerialNumber = serialNumber,
                DataElementType = FSSHTTPBSerializer.CreateCompactUint64((ulong)DataElementTypes.RevisionManifest),
                RevisionManifest = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.RevisionManifest,
                    RevisionManifestLength(currentRevisionGuid, baseRevisionGuid), 1),
                RevisionID = currentRevisionGuid,
                BaseRevisionID = baseRevisionGuid,
                RevisionManifestDataElementsData = subElements.ToArray(),
                DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElement)
            };
        }

        /// <summary>
        /// Build root object group – contains an intermediate node object
        /// that references all leaf chunk objects.
        /// Per MS-FSSHTTPD 2.2.2.1: IntermediateNodeObject contains Signature + DataSize.
        /// </summary>
        private static ObjectGroupDataElements BuildRootObjectGroup(
            ExtendedGUID objectGroupGuid,
            SerialNumber serialNumber,
            ExtendedGUID rootObjectGuid,
            List<ExtendedGUID> childObjectGuids,
            byte[] fileBytes)
        {
            // Declaration for root object (intermediate node)
            ulong referencesCount = (ulong)childObjectGuids.Count;

            // The Inner Data Size is the total uncompressed file size
            ulong totalFileSize = (ulong)fileBytes.LongLength;

            var rootDecl = new ObjectDeclaration
            {
                ObjectGroupObjectDeclaration = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupObjectDeclare,
                    ObjectDeclareLength(rootObjectGuid, 1, 0, referencesCount, 0)),
                ObjectExtendedGUID = rootObjectGuid,
                ObjectPartitionID = FSSHTTPBSerializer.CreateCompactUint64(1),
                ObjectDataSize = FSSHTTPBSerializer.CreateCompactUint64(0), // Intermediate node data size is in the sub-structure
                ObjectReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(referencesCount),
                CellReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(0)
            };

            // Build Object Data for root (intermediate node)
            // Per MS-FSSHTTPD 2.2.2.1: IntermediateNodeObject has Signature + DataSize
            // The root intermediate node signature is the SHA-1 hash of the entire file content.
            // Ref: OfficeDev/Interop-TestSuites NodeObject.cs RootNodeObjectBuilder.Build(byte[])
            byte[] signatureBytes = Sha1(fileBytes);

            // Build the IntermediateNodeObjectData binary using the shared helper
            // (consistent with BuildIntermediateNodeBytes used for non-root intermediate nodes)
            byte[] intermediateNodeBytes = BuildIntermediateNodeBytes(signatureBytes, totalFileSize);

            // Update root declaration data size
            rootDecl.ObjectDataSize = FSSHTTPBSerializer.CreateCompactUint64((ulong)intermediateNodeBytes.Length);
            // Recompute ObjectDeclare header with actual data size
            rootDecl.ObjectGroupObjectDeclaration = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.ObjectGroupObjectDeclare,
                ObjectDeclareLength(rootObjectGuid, 1, (ulong)intermediateNodeBytes.Length, referencesCount, 0));

            // Build ExtendedGUIDArray for references (child object GUIDs)
            var refGuids = new ExtendedGUIDArray
            {
                Count = FSSHTTPBSerializer.CreateCompactUint64(referencesCount),
                Content = childObjectGuids.ToArray()
            };

            var rootData = new ObjectData
            {
                ObjectGroupObjectDataOrExcludedData = CreateObjectDataHeader(
                    childObjectGuids.ToArray(), (ulong)intermediateNodeBytes.Length),
                ObjectExtendedGUIDArray = refGuids,
                CellIDArray = new CellIDArray
                {
                    Count = FSSHTTPBSerializer.CreateCompactUint64(0)
                },
                DataSize = FSSHTTPBSerializer.CreateCompactUint64((ulong)intermediateNodeBytes.Length),
                Data = intermediateNodeBytes
            };

            return new ObjectGroupDataElements
            {
                DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElement,
                    DataElementStartLength(objectGroupGuid, serialNumber, (ulong)DataElementTypes.ObjectGroup), 1),
                DataElementExtendedGUID = objectGroupGuid,
                SerialNumber = serialNumber,
                DataElementType = FSSHTTPBSerializer.CreateCompactUint64((ulong)DataElementTypes.ObjectGroup),
                ObjectGroupDeclarationsStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupDeclarations, 0, 1),
                ObjectDeclarationOrObjectDataBLOBDeclaration = new object[] { rootDecl },
                ObjectGroupDeclarationsEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.ObjectGroupDeclarations),
                ObjectGroupDataStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupData, 0, 1),
                ObjectDataOrObjectDataBLOBReference = new object[] { rootData },
                ObjectGroupDataEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.ObjectGroupData),
                DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElement)
            };
        }

        /// <summary>
        /// Build object groups for a chunk according to MS-FSSHTTPD 2.4.1:
        /// - chunk <= 3 MB: one leaf node object
        /// - chunk > 3 MB: parent intermediate node + subchunk leaf nodes (<= 3 MB)
        /// - each leaf with DataSize <= 1 MB references a data node object
        /// </summary>
        private static List<ObjectGroupDataElements> BuildChunkObjectGroups(
            Guid storageGuid,
            SerialNumber serialNumber,
            AnalyzedChunk chunk,
            ref uint exGuidCounter,
            ref ulong uniqueSignatureCounter,
            out ExtendedGUID topObjectGuid)
        {
            var objectGroups = new List<ObjectGroupDataElements>();

            const int maxSubchunkSize = 3 * 1024 * 1024;
            if (chunk.Bytes.Length <= maxSubchunkSize)
            {
                var objectGroupGuid = NextExGuid(storageGuid, ref exGuidCounter);
                var leafObjectGuid = NextExGuid(storageGuid, ref exGuidCounter);
                var leafGroup = BuildLeafObjectGroup(
                    storageGuid,
                    objectGroupGuid,
                    serialNumber,
                    leafObjectGuid,
                    chunk.Bytes,
                    chunk.Signature,
                    ref exGuidCounter);

                topObjectGuid = leafObjectGuid;
                objectGroups.Add(leafGroup);
                return objectGroups;
            }

            var subchunkTopObjectIds = new List<ExtendedGUID>();
            for (int offset = 0; offset < chunk.Bytes.Length; offset += maxSubchunkSize)
            {
                int size = Math.Min(maxSubchunkSize, chunk.Bytes.Length - offset);
                var subchunkBytes = SliceBytes(chunk.Bytes, offset, size);

                var subchunkSignature = NextUniqueSubchunkSignature(ref uniqueSignatureCounter);
                var subObjectGroupGuid = NextExGuid(storageGuid, ref exGuidCounter);
                var subLeafObjectGuid = NextExGuid(storageGuid, ref exGuidCounter);

                var subLeafGroup = BuildLeafObjectGroup(
                    storageGuid,
                    subObjectGroupGuid,
                    serialNumber,
                    subLeafObjectGuid,
                    subchunkBytes,
                    subchunkSignature,
                    ref exGuidCounter);

                subchunkTopObjectIds.Add(subLeafObjectGuid);
                objectGroups.Add(subLeafGroup);
            }

            var parentObjectGroupGuid = NextExGuid(storageGuid, ref exGuidCounter);
            var parentObjectGuid = NextExGuid(storageGuid, ref exGuidCounter);
            var parentIntermediateBytes = BuildIntermediateNodeBytes(chunk.Signature, (ulong)chunk.Bytes.Length);

            var parentDeclaration = new ObjectDeclaration
            {
                ObjectGroupObjectDeclaration = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupObjectDeclare,
                    ObjectDeclareLength(parentObjectGuid, 1, (ulong)parentIntermediateBytes.Length, (ulong)subchunkTopObjectIds.Count, 0)),
                ObjectExtendedGUID = parentObjectGuid,
                ObjectPartitionID = FSSHTTPBSerializer.CreateCompactUint64(1),
                ObjectDataSize = FSSHTTPBSerializer.CreateCompactUint64((ulong)parentIntermediateBytes.Length),
                ObjectReferencesCount = FSSHTTPBSerializer.CreateCompactUint64((ulong)subchunkTopObjectIds.Count),
                CellReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(0)
            };

            var parentData = new ObjectData
            {
                ObjectGroupObjectDataOrExcludedData = CreateObjectDataHeader(
                    subchunkTopObjectIds.ToArray(), (ulong)parentIntermediateBytes.Length),
                ObjectExtendedGUIDArray = new ExtendedGUIDArray
                {
                    Count = FSSHTTPBSerializer.CreateCompactUint64((ulong)subchunkTopObjectIds.Count),
                    Content = subchunkTopObjectIds.ToArray()
                },
                CellIDArray = new CellIDArray
                {
                    Count = FSSHTTPBSerializer.CreateCompactUint64(0)
                },
                DataSize = FSSHTTPBSerializer.CreateCompactUint64((ulong)parentIntermediateBytes.Length),
                Data = parentIntermediateBytes
            };

            var parentObjectGroup = new ObjectGroupDataElements
            {
                DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElement,
                    DataElementStartLength(parentObjectGroupGuid, serialNumber, (ulong)DataElementTypes.ObjectGroup), 1),
                DataElementExtendedGUID = parentObjectGroupGuid,
                SerialNumber = serialNumber,
                DataElementType = FSSHTTPBSerializer.CreateCompactUint64((ulong)DataElementTypes.ObjectGroup),
                ObjectGroupDeclarationsStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupDeclarations, 0, 1),
                ObjectDeclarationOrObjectDataBLOBDeclaration = new object[] { parentDeclaration },
                ObjectGroupDeclarationsEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.ObjectGroupDeclarations),
                ObjectGroupDataStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupData, 0, 1),
                ObjectDataOrObjectDataBLOBReference = new object[] { parentData },
                ObjectGroupDataEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.ObjectGroupData),
                DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElement)
            };

            // Parent first so references resolve naturally in stream order.
            objectGroups.Insert(0, parentObjectGroup);
            topObjectGuid = parentObjectGuid;
            return objectGroups;
        }

        private static ObjectGroupDataElements BuildLeafObjectGroup(
            Guid storageGuid,
            ExtendedGUID objectGroupGuid,
            SerialNumber serialNumber,
            ExtendedGUID leafObjectGuid,
            byte[] representedBytes,
            byte[] leafSignature,
            ref uint exGuidCounter)
        {
            var leafNodeBytes = BuildLeafNodeBytes(leafSignature, (ulong)representedBytes.Length);

            var declarations = new List<object>();
            var objectData = new List<object>();

            // Data Node Object must be created for each leaf <= 1 MB.
            ExtendedGUID dataNodeGuid = null;
            if (representedBytes.Length <= 1048576)
            {
                dataNodeGuid = NextExGuid(storageGuid, ref exGuidCounter);

                var dataNodeDeclaration = new ObjectDeclaration
                {
                    ObjectGroupObjectDeclaration = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                        StreamObjectTypeHeaderStart.ObjectGroupObjectDeclare,
                        ObjectDeclareLength(dataNodeGuid, 1, (ulong)representedBytes.Length, 0, 0)),
                    ObjectExtendedGUID = dataNodeGuid,
                    ObjectPartitionID = FSSHTTPBSerializer.CreateCompactUint64(1),
                    ObjectDataSize = FSSHTTPBSerializer.CreateCompactUint64((ulong)representedBytes.Length),
                    ObjectReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(0),
                    CellReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(0)
                };
                declarations.Add(dataNodeDeclaration);

                var dataNodeObjectData = new ObjectData
                {
                    ObjectGroupObjectDataOrExcludedData = CreateObjectDataHeader(
                        null, (ulong)representedBytes.Length),
                    ObjectExtendedGUIDArray = new ExtendedGUIDArray
                    {
                        Count = FSSHTTPBSerializer.CreateCompactUint64(0)
                    },
                    CellIDArray = new CellIDArray
                    {
                        Count = FSSHTTPBSerializer.CreateCompactUint64(0)
                    },
                    DataSize = FSSHTTPBSerializer.CreateCompactUint64((ulong)representedBytes.Length),
                    Data = representedBytes
                };
                objectData.Add(dataNodeObjectData);
            }

            ulong leafRefCount = dataNodeGuid == null ? 0UL : 1UL;
            var leafDeclaration = new ObjectDeclaration
            {
                ObjectGroupObjectDeclaration = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupObjectDeclare,
                    ObjectDeclareLength(leafObjectGuid, 1, (ulong)leafNodeBytes.Length, leafRefCount, 0)),
                ObjectExtendedGUID = leafObjectGuid,
                ObjectPartitionID = FSSHTTPBSerializer.CreateCompactUint64(1),
                ObjectDataSize = FSSHTTPBSerializer.CreateCompactUint64((ulong)leafNodeBytes.Length),
                ObjectReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(leafRefCount),
                CellReferencesCount = FSSHTTPBSerializer.CreateCompactUint64(0)
            };
            declarations.Insert(0, leafDeclaration);

            var leafRefGuids = dataNodeGuid == null ? null : new[] { dataNodeGuid };
            var leafObjectData = new ObjectData
            {
                ObjectGroupObjectDataOrExcludedData = CreateObjectDataHeader(
                    leafRefGuids, (ulong)leafNodeBytes.Length),
                ObjectExtendedGUIDArray = new ExtendedGUIDArray
                {
                    Count = FSSHTTPBSerializer.CreateCompactUint64(leafRefCount),
                    Content = leafRefGuids
                },
                CellIDArray = new CellIDArray
                {
                    Count = FSSHTTPBSerializer.CreateCompactUint64(0)
                },
                DataSize = FSSHTTPBSerializer.CreateCompactUint64((ulong)leafNodeBytes.Length),
                Data = leafNodeBytes
            };
            objectData.Insert(0, leafObjectData);

            return new ObjectGroupDataElements
            {
                DataElementStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElement,
                    DataElementStartLength(objectGroupGuid, serialNumber, (ulong)DataElementTypes.ObjectGroup), 1),
                DataElementExtendedGUID = objectGroupGuid,
                SerialNumber = serialNumber,
                DataElementType = FSSHTTPBSerializer.CreateCompactUint64((ulong)DataElementTypes.ObjectGroup),
                ObjectGroupDeclarationsStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupDeclarations, 0, 1),
                ObjectDeclarationOrObjectDataBLOBDeclaration = declarations.ToArray(),
                ObjectGroupDeclarationsEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.ObjectGroupDeclarations),
                ObjectGroupDataStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupData, 0, 1),
                ObjectDataOrObjectDataBLOBReference = objectData.ToArray(),
                ObjectGroupDataEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.ObjectGroupData),
                DataElementEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.DataElement),

                DataElementHash = new DataElementHash()
                {
                    DataElementHashDeclaration = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataElementHash,
                    DataElementHashLength(1, leafSignature), compound: 0),

                    // 2. Data Element Hash Scheme: Must be 1 (Content Information Data Structure Version 1.0)
                    DataElementHashScheme = FSSHTTPBSerializer.CreateCompactUint64(1),

                    // 3. Data Element Hash Data: The actual SHA-1 hash bytes wrapped in a BinaryItem
                    DataElementHashData = new BinaryItem
                    {
                        Length = FSSHTTPBSerializer.CreateCompactUint64((ulong)leafSignature.Length),
                        Content = leafSignature
                    }
                }
            };
        }

        private static byte[] BuildLeafNodeBytes(byte[] signature, ulong dataSize)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // Per MS-FSSHTTPB 2.2.1.5.1: non-compound 16-bit header Length = payload bytes.
            // SignatureObject payload = BinaryItem(CompactUint64 length + content bytes).
            byte signaturePayloadLength = ComputeBinaryItemSerializedLength(signature);

            var leaf = new LeafNodeObjectData
            {
                LeafNodeStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.LeafNodeObject, 0, 1),
                SignatureHeader = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.SignatureObject, signaturePayloadLength),
                SignatureData = new BinaryItem
                {
                    Length = FSSHTTPBSerializer.CreateCompactUint64((ulong)signature.Length),
                    Content = signature
                },
                DataSizeHeader = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataSizeObject, 8),
                DataSize = dataSize,
                LeafNodeEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.LeafNodeEnd),
            };

            leaf.Serialize(writer);
            writer.Flush();
            return ms.ToArray();
        }

        private static byte[] BuildIntermediateNodeBytes(byte[] signature, ulong dataSize)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // Per MS-FSSHTTPB 2.2.1.5.1: non-compound 16-bit header Length = payload bytes.
            byte signaturePayloadLength = ComputeBinaryItemSerializedLength(signature);

            var node = new IntermediateNodeObjectData
            {
                IntermediateNodeStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.IntermediateNodeObject, 0, 1),
                SignatureHeader = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.SignatureObject, signaturePayloadLength),
                SignatureData = new BinaryItem
                {
                    Length = FSSHTTPBSerializer.CreateCompactUint64((ulong)signature.Length),
                    Content = signature
                },
                DataSizeHeader = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.DataSizeObject, 8),
                DataSize = dataSize,
                IntermediateNodeEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.IntermediateNodeEnd),
            };

            node.Serialize(writer);
            writer.Flush();
            return ms.ToArray();
        }

        private static byte[] NextUniqueSubchunkSignature(ref ulong counter)
        {
            ulong current = counter++;
            return BitConverter.GetBytes(current);
        }

        #endregion

        #region Knowledge Builder

        private static Knowledge BuildKnowledge(Guid cellStorageGuid, ulong serialVal)
        {
            // Cell Knowledge
            var cellKnowledgeRange = new CellKnowledgeRange
            {
                cellKnowledgeRange = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.CellKnowledgeRange,
                    CellKnowledgeRangeLength(0, serialVal)),
                GUID = cellStorageGuid,
                From = FSSHTTPBSerializer.CreateCompactUint64(0),
                To = FSSHTTPBSerializer.CreateCompactUint64(serialVal)
            };

            var cellKnowledge = new CellKnowLedge
            {
                CellKnowledgeStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.CellKnowledge, 0, 1),
                CellKnowledgeData = new object[] { cellKnowledgeRange },
                CellKnowledgeEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.CellKnowledge)
            };

            var cellKnowledgeSK = new SpecializedKnowledge
            {
                SpecializedKnowledgeStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.SpecializedKnowledge, 16, 1),
                GUID = CellKnowledgeGuid,
                SpecializedKnowledgeData = cellKnowledge,
                SpecializedKnowledgeEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.SpecializedKnowledge)
            };

            // Waterline Knowledge
            var cellStorageExGuid = CreateExGuid5Bit(cellStorageGuid, 1);
            var waterlineEntry = new WaterlineKnowledgeEntry
            {
                waterlineKnowledgeEntry = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.WaterlineKnowledgeEntry,
                    WaterlineKnowledgeEntryLength(cellStorageExGuid, serialVal, 0)),
                CellStorageExtendedGUID = cellStorageExGuid,
                Waterline = FSSHTTPBSerializer.CreateCompactUint64(serialVal),
                Reserved = FSSHTTPBSerializer.CreateCompactUint64(0)
            };

            var waterlineKnowledge = new WaterlineKnowledge
            {
                WaterlineKnowledgeStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.WaterlineKnowledge, 0, 1),
                WaterlineKnowledgeData = new[] { waterlineEntry },
                WaterlineKnowledgeEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.WaterlineKnowledge)
            };

            var waterlineSK = new SpecializedKnowledge
            {
                SpecializedKnowledgeStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.SpecializedKnowledge, 16, 1),
                GUID = WaterlineKnowledgeGuid,
                SpecializedKnowledgeData = waterlineKnowledge,
                SpecializedKnowledgeEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.SpecializedKnowledge)
            };

            // Content Tag Knowledge (empty – no content tags initially)
            var contentTagKnowledge = new ContentTagKnowledge
            {
                ContentTagKnowledgeStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ContentTagKnowledge, 0, 1),
                ContentTagKnowledgeEntryArray = Array.Empty<ContentTagKnowledgeEntry>(),
                ContentTagKnowledgeEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.ContentTagKnowledge)
            };

            var contentTagSK = new SpecializedKnowledge
            {
                SpecializedKnowledgeStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.SpecializedKnowledge, 16, 1),
                GUID = ContentTagKnowledgeGuid,
                SpecializedKnowledgeData = contentTagKnowledge,
                SpecializedKnowledgeEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.SpecializedKnowledge)
            };

            return new Knowledge
            {
                KnowledgeStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.Knowledge, 0, 1),
                SpecializedKnowledge = new[] { cellKnowledgeSK, waterlineSK, contentTagSK },
                KnowledgeEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                    StreamObjectTypeHeaderEnd.Knowledge)
            };
        }

        private static Knowledge BuildEmptyKnowledge()
        {
            return new Knowledge
            {
                KnowledgeStart = FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                   StreamObjectTypeHeaderStart.Knowledge, 0, 1),
                KnowledgeEnd = FSSHTTPBSerializer.Create8BitStreamObjectHeaderEnd(
                   StreamObjectTypeHeaderEnd.Knowledge)
            };
        }

        #endregion

        #region Helper: ExtendedGUID factory

        private static ExtendedGUID5BitUintValue CreateExGuid5Bit(Guid guid, int value)
        {
            return new ExtendedGUID5BitUintValue
            {
                Type = 0x04,
                Value = (byte)value,
                GUID = guid
            };
        }

        private static ExtendedGUID32BitUintValue NextExGuid(Guid guid, ref uint value)
        {
            return new ExtendedGUID32BitUintValue
            {
                Type = 0x80,
                Value = value++,
                GUID = guid
            };
        }

        #endregion

        #region Helper: BinaryItem serialized length

        /// <summary>
        /// Compute the serialized byte length of a BinaryItem for a given content byte array.
        /// BinaryItem = CompactUnsigned64bitInteger(length) + content bytes.
        /// Used to set the Length field in non-compound 16-bit stream object headers.
        /// </summary>
        private static byte ComputeBinaryItemSerializedLength(byte[] content)
        {
            int contentLength = content?.Length ?? 0;
            int compactUintSize = GetCompactUint64SerializedSize((ulong)contentLength);
            int total = compactUintSize + contentLength;
            // 16-bit header Length field is 7 bits (max 127). If total exceeds 127,
            // the caller must use a 32-bit header instead. For typical signatures (≤20 bytes),
            // this will always fit.
            return (byte)Math.Min(total, 127);
        }

        /// <summary>
        /// Returns the serialized byte size of a CompactUnsigned64bitInteger for a given value.
        /// Per MS-FSSHTTPB 2.2.1.1.
        /// </summary>
        private static int GetCompactUint64SerializedSize(ulong value)
        {
            if (value == 0) return 1;
            if (value <= 0x7F) return 1;
            if (value <= 0x3FFF) return 2;
            if (value <= 0x1FFFFF) return 3;
            if (value <= 0x0FFFFFFF) return 4;
            if (value <= 0x7FFFFFFFF) return 5;
            if (value <= 0x3FFFFFFFFFF) return 6;
            if (value <= 0x1FFFFFFFFFFFF) return 7;
            return 9;
        }

        /// <summary>
        /// Returns the serialized byte size of an ExtendedGUID.
        /// Per MS-FSSHTTPB 2.2.1.7:
        ///   Null (Type=0x00): 1 byte
        ///   5-bit  (Type=0x04): 1 + 16 = 17 bytes
        ///   10-bit (Type=0x20): 2 + 16 = 18 bytes
        ///   17-bit (Type=0x40): 3 + 16 = 19 bytes
        ///   32-bit (Type=0x80): 1 + 4 + 16 = 21 bytes
        /// </summary>
        private static int ExGuidSize(ExtendedGUID exGuid)
        {
            if (exGuid is ExtendedGUIDNullValue) return 1;
            if (exGuid is ExtendedGUID5BitUintValue) return 17;
            if (exGuid is ExtendedGUID10BitUintValue) return 18;
            if (exGuid is ExtendedGUID17BitUintValue) return 19;
            if (exGuid is ExtendedGUID32BitUintValue) return 21;
            throw new InvalidOperationException("Unknown ExtendedGUID subtype for size calculation.");
        }

        /// <summary>
        /// Returns the serialized byte size of a SerialNumber.
        /// Per MS-FSSHTTPB 2.2.1.9:
        ///   Null (Type=0x00): 1 byte
        ///   64-bit (Type=0x80): 1 + 16 + 8 = 25 bytes
        /// </summary>
        private static int SerialNumberSize(SerialNumber sn)
        {
            if (sn is SerialNumberNullValue) return 1;
            if (sn is SerialNumber64BitUintValue) return 25;
            throw new InvalidOperationException("Unknown SerialNumber subtype for size calculation.");
        }

        /// <summary>
        /// Returns the serialized byte size of a CellID (two ExtendedGUIDs).
        /// Per MS-FSSHTTPB 2.2.1.10.
        /// </summary>
        private static int CellIdSize(CellID cellId)
        {
            return ExGuidSize(cellId.EXGUID1) + ExGuidSize(cellId.EXGUID2);
        }

        /// <summary>
        /// Computes the immediate body length for a DataElement SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.1:
        ///   Immediate fields: DataElementExtendedGUID + SerialNumber + DataElementType(CompactUint64)
        /// </summary>
        private static byte DataElementStartLength(ExtendedGUID exGuid, SerialNumber sn, ulong dataElementType)
        {
            return (byte)(ExGuidSize(exGuid) + SerialNumberSize(sn) + GetCompactUint64SerializedSize(dataElementType));
        }

        /// <summary>
        /// Computes the immediate body length for an ObjectGroupObjectDeclare SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.6.1:
        ///   Immediate fields: ObjectExtendedGUID + ObjectPartitionID(CUint64) +
        ///                     ObjectDataSize(CUint64) + ObjectReferencesCount(CUint64) +
        ///                     CellReferencesCount(CUint64)
        /// </summary>
        private static byte ObjectDeclareLength(ExtendedGUID objectGuid, ulong partitionId, ulong dataSize, ulong refCount, ulong cellRefCount)
        {
            return (byte)(ExGuidSize(objectGuid)
                + GetCompactUint64SerializedSize(partitionId)
                + GetCompactUint64SerializedSize(dataSize)
                + GetCompactUint64SerializedSize(refCount)
                + GetCompactUint64SerializedSize(cellRefCount));
        }

        /// <summary>
        /// Computes the immediate body length for an ObjectGroupObjectData SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.6.4:
        ///   Immediate fields: ExtendedGUIDArray + CellIDArray + DataSize(CUint64) + Data bytes
        /// Uses 16-bit SOH when total <= 127 bytes, otherwise 32-bit SOH.
        /// </summary>
        private static int ObjectDataImmediateLength(ExtendedGUID[] refGuids, ulong dataSize)
        {
            int refCount = refGuids?.Length ?? 0;
            int exGuidArraySize = GetCompactUint64SerializedSize((ulong)refCount);
            if (refGuids != null)
            {
                foreach (var g in refGuids)
                    exGuidArraySize += ExGuidSize(g);
            }
            int cellIdArraySize = GetCompactUint64SerializedSize(0); // always 0 cell refs = 1 byte
            int dataSizeFieldSize = GetCompactUint64SerializedSize(dataSize);
            return exGuidArraySize + cellIdArraySize + dataSizeFieldSize + (int)dataSize;
        }

        /// <summary>
        /// Computes the immediate body length for StorageIndexCellMapping SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.2:
        ///   Immediate fields: CellID(EXGUID1+EXGUID2) + CellMappingExtendedGUID + CellMappingSerialNumber
        /// </summary>
        private static byte StorageIndexCellMappingLength(CellID cellId, ExtendedGUID mappingGuid, SerialNumber sn)
        {
            return (byte)(CellIdSize(cellId) + ExGuidSize(mappingGuid) + SerialNumberSize(sn));
        }

        /// <summary>
        /// Computes the immediate body length for StorageIndexManifestMapping SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.2:
        ///   Immediate fields: ManifestMappingExtendedGUID + ManifestMappingSerialNumber
        /// </summary>
        private static byte StorageIndexManifestMappingLength(ExtendedGUID manifestGuid, SerialNumber sn)
        {
            return (byte)(ExGuidSize(manifestGuid) + SerialNumberSize(sn));
        }

        /// <summary>
        /// Computes the immediate body length for StorageIndexRevisionMapping SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.2:
        ///   Immediate fields: RevisionExtendedGUID + RevisionMappingExtendedGUID + RevisionMappingSerialNumber
        /// </summary>
        private static byte StorageIndexRevisionMappingLength(ExtendedGUID revisionGuid, ExtendedGUID mappingGuid, SerialNumber sn)
        {
            return (byte)(ExGuidSize(revisionGuid) + ExGuidSize(mappingGuid) + SerialNumberSize(sn));
        }

        /// <summary>
        /// Computes the immediate body length for StorageManifestRootDeclare SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.3:
        ///   Immediate fields: RootExtendedGUID + CellID(EXGUID1+EXGUID2)
        /// </summary>
        private static byte StorageManifestRootDeclareLength(ExtendedGUID rootGuid, CellID cellId)
        {
            return (byte)(ExGuidSize(rootGuid) + CellIdSize(cellId));
        }

        /// <summary>
        /// Computes the immediate body length for CellManifestCurrentRevision SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.4:
        ///   Immediate fields: CellManifestCurrentRevisionExtendedGUID
        /// </summary>
        private static byte CellManifestCurrentRevisionLength(ExtendedGUID revisionGuid)
        {
            return (byte)ExGuidSize(revisionGuid);
        }

        /// <summary>
        /// Computes the immediate body length for RevisionManifest SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.5 (compound):
        ///   Immediate fields: RevisionID(ExGuid) + BaseRevisionID(ExGuid)
        /// </summary>
        private static byte RevisionManifestLength(ExtendedGUID revisionId, ExtendedGUID baseRevisionId)
        {
            return (byte)(ExGuidSize(revisionId) + ExGuidSize(baseRevisionId));
        }

        /// <summary>
        /// Computes the immediate body length for RevisionManifestRootDeclare SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.5:
        ///   Immediate fields: RootExtendedGUID + ObjectExtendedGUID
        /// </summary>
        private static byte RevisionManifestRootDeclareLength(ExtendedGUID rootGuid, ExtendedGUID objectGuid)
        {
            return (byte)(ExGuidSize(rootGuid) + ExGuidSize(objectGuid));
        }

        /// <summary>
        /// Computes the immediate body length for RevisionManifestObjectGroupReferences SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.5:
        ///   Immediate fields: ObjectGroupExtendedGUID
        /// </summary>
        private static byte RevisionManifestObjectGroupReferencesLength(ExtendedGUID objectGroupGuid)
        {
            return (byte)ExGuidSize(objectGroupGuid);
        }

        /// <summary>
        /// Computes the immediate body length for CellKnowledgeRange SOH.
        /// Per MS-FSSHTTPB 2.2.1.13.2.1:
        ///   Immediate fields: GUID(16 bytes) + From(CUint64) + To(CUint64)
        /// </summary>
        private static byte CellKnowledgeRangeLength(ulong from, ulong to)
        {
            return (byte)(16 + GetCompactUint64SerializedSize(from) + GetCompactUint64SerializedSize(to));
        }

        /// <summary>
        /// Computes the immediate body length for WaterlineKnowledgeEntry SOH.
        /// Per MS-FSSHTTPB 2.2.1.13.4.1:
        ///   Immediate fields: CellStorageExtendedGUID + Waterline(CUint64) + Reserved(CUint64)
        /// </summary>
        private static byte WaterlineKnowledgeEntryLength(ExtendedGUID cellStorageGuid, ulong waterline, ulong reserved)
        {
            return (byte)(ExGuidSize(cellStorageGuid) + GetCompactUint64SerializedSize(waterline) + GetCompactUint64SerializedSize(reserved));
        }

        /// <summary>
        /// Computes the immediate body length for QueryChangesResponse SOH.
        /// Per MS-FSSHTTPB 2.2.2.1.2 (compound):
        ///   Immediate fields: StorageIndexExtendedGUID + P(1 byte, packed with Reserved)
        /// Note: P and Reserved are packed into a single byte.
        /// </summary>
        private static short QueryChangesResponseLength(ExtendedGUID storageIndexGuid)
        {
            return (short)(ExGuidSize(storageIndexGuid) + 2);
        }

        /// <summary>
        /// Computes the immediate body length for DataElementHash SOH.
        /// Per MS-FSSHTTPB 2.2.1.12.6.6:
        ///   Immediate fields: DataElementHashScheme(CUint64) + DataElementHashData(BinaryItem)
        /// </summary>
        private static byte DataElementHashLength(ulong scheme, byte[] hashData)
        {
            return (byte)(GetCompactUint64SerializedSize(scheme) + ComputeBinaryItemSerializedLength(hashData));
        }

        /// <summary>
        /// Computes the immediate body length for FsshttpbSubResponse SOH.
        /// Per MS-FSSHTTPB 2.2.3.1 (compound):
        ///   Immediate fields: RequestID(CUint64) + RequestType(CUint64) + Status/Reserved(1 byte)
        /// </summary>
        private static short SubResponseLength(ulong requestId, ulong requestType)
        {
            return (short)(GetCompactUint64SerializedSize(requestId) + GetCompactUint64SerializedSize(requestType) + 1);
        }

        /// <summary>
        /// Creates the correct SOH for ObjectGroupObjectData.
        /// Per MS-FSSHTTPB 2.2.1.12.6.4 (non-compound):
        ///   Immediate fields: ExtendedGUIDArray + CellIDArray + DataSize(CUint64) + Data bytes
        /// Uses 16-bit SOH when total <= 127 bytes, otherwise 32-bit SOH.
        /// </summary>
        private static StreamObjectHeader CreateObjectDataHeader(ExtendedGUID[] refGuids, ulong dataSize)
        {
            int immediateLength = ObjectDataImmediateLength(refGuids, dataSize);
            if (immediateLength <= 127)
            {
                return FSSHTTPBSerializer.Create16BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupObjectData, (byte)immediateLength);
            }
            else if (immediateLength <= 32766)
            {
                return FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupObjectData, (short)immediateLength);
            }
            else
            {
                // Length > 32766: set header length to 32767 and append LargeLength
                return FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                    StreamObjectTypeHeaderStart.ObjectGroupObjectData, 32767,
                    largeLength: FSSHTTPBSerializer.CreateCompactUint64((ulong)immediateLength));
            }
        }

        #endregion

        #region Editors Table

        /// <summary>
        /// Builds the binary payload for an Editors Table response.
        /// Prepends the mandatory 8-byte Editors Table Zip Stream Header,
        /// then appends the DEFLATE-compressed UTF-8 XML content.
        /// </summary>
        /// <param name="xmlString">The XML string representing the editors table.</param>
        /// <returns>Header concatenated with DEFLATE-compressed XML bytes.</returns>
        public static byte[] BuildEditorsTablePayload(string xmlString)
        {
            // 1. The mandatory 8-byte Editors Table Zip Stream Header
            byte[] header = new byte[] { 0x1A, 0x5A, 0x3A, 0x30, 0x00, 0x00, 0x00, 0x00 };

            // 2. Convert XML to UTF-8 bytes
            byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlString);

            // 3. Compress using DEFLATE
            using var memoryStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress, true))
            {
                deflateStream.Write(xmlBytes, 0, xmlBytes.Length);
            }
            byte[] compressedXml = memoryStream.ToArray();

            // 4. Combine header and compressed data
            byte[] finalPayload = new byte[header.Length + compressedXml.Length];
            Buffer.BlockCopy(header, 0, finalPayload, 0, header.Length);
            Buffer.BlockCopy(compressedXml, 0, finalPayload, header.Length, compressedXml.Length);

            return finalPayload;
        }

        /// <summary>
        /// Reads an XML file from the wwwroot/files folder and builds the Editors Table binary payload.
        /// </summary>
        /// <param name="webRootPath">The web root path (IWebHostEnvironment.WebRootPath).</param>
        /// <param name="xmlFileName">The XML file name inside wwwroot/files. Defaults to "EditorsTable.xml".</param>
        /// <returns>The binary payload ready for use in a LeafNodeObject.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the XML file does not exist.</exception>
        public static byte[] BuildEditorsTablePayloadFromFile(string webRootPath, string xmlFileName = "EditorsTable.xml")
        {
            var filePath = Path.Combine(webRootPath, "files", xmlFileName);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Editors Table XML file not found at '{filePath}'.", filePath);

            var xmlString = File.ReadAllText(filePath, Encoding.UTF8);
            return BuildEditorsTablePayload(xmlString);
        }

        #endregion
    }
}
