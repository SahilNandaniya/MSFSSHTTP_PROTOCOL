using MSFSSHTTP.Parsers;
using MSFSSHTTP.Services;
using Xunit;

namespace MSFSSHTTP.Tests;

public class FsshttpbAggregateTests
{
    [Fact]
    public void PutChangesAck_FullFsshttpbResponse_RoundTrips_Parse()
    {
        var session = Guid.NewGuid();
        var ex = new ExtendedGUID32BitUintValue { Type = 0x80, Value = 1, GUID = session };
        var putReq = new PutChangesRequest
        {
            StorageIndexExtendedGUID = ex,
            ExpectedStorageIndexExtendedGUID = ex,
            ClientKnowledge = null
        };

        var sub = FSSHTTPBResponseBuilder.CreatePutChangesSubResponse(3, putReq);
        var bytes = FSSHTTPBResponseBuilder.BuildFsshttpbResponseBytes(new[] { sub });

        var resp = new FsshttpbResponse();
        resp.Parse(new MemoryStream(bytes));

        Assert.Equal((byte)0, resp.Status);
        Assert.NotNull(resp.SubResponses);
        Assert.Single(resp.SubResponses!);

        var uintReader = new bit32StreamObjectHeaderStart();
        Assert.Equal(5ul, uintReader.GetUint(resp.SubResponses![0].RequestType));
        Assert.Equal(3ul, uintReader.GetUint(resp.SubResponses[0].RequestID));
    }

    [Fact]
    public void CellDispatcher_QueryAccessOnly_Produces_ValidResponse()
    {
        var req = new FsshttpbRequest
        {
            ProtocolVersion = 13,
            MinimumVersion = 11,
            Signature = 0x9B069439F329CF9D,
            RequestStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                StreamObjectTypeHeaderStart.Request,
                1,
                1),
            UserAgentStart = null,
            SubRequest = new[]
            {
                new FsshttpbSubRequest
                {
                    SubRequestStart = FSSHTTPBSerializer.Create32BitStreamObjectHeaderStart(
                        StreamObjectTypeHeaderStart.SubRequest,
                        4,
                        1),
                    RequestID = FSSHTTPBSerializer.CreateCompactUint64(1),
                    RequestType = FSSHTTPBSerializer.CreateCompactUint64(1),
                    Priority = FSSHTTPBSerializer.CreateCompactUint64(0),
                    SubRequestData = new QueryAccessRequest(),
                    SubRequestEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(
                        StreamObjectTypeHeaderEnd.SubRequest)
                }
            },
            DataElementPackage = null,
            RequestEnd = FSSHTTPBSerializer.Create16BitStreamObjectHeaderEnd(StreamObjectTypeHeaderEnd.Request)
        };

        using var msReq = new MemoryStream();
        using (var w = new BinaryWriter(msReq))
        {
            req.Serialize(w);
        }

        var outBytes = CellFsshttpbDispatcher.BuildAggregatedResponse(msReq.ToArray());
        var parsed = new FsshttpbResponse();
        parsed.Parse(new MemoryStream(outBytes));
        Assert.Equal((byte)0, parsed.Status);
        Assert.NotNull(parsed.SubResponses);
        Assert.Single(parsed.SubResponses!);
    }
}
