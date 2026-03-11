using MSFSSHTTP.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace MSFSSHTTP.Parsers
{
    public abstract class FSSHTTPandWOPIInspector
    {
        /// <summary>
        /// Gets or sets the Tree View control where displayed the FSSHTTPandWOPI message.
        /// </summary>
        //public TreeView FSSHTTPandWOPIViewControl { get; set; }

        /// <summary>
        /// Gets or sets the control collection where displayed the FSSHTTPandWOPI parsed message and corresponding hex data.
        /// </summary>
        //public FSSHTTPandWOPIControl FSSHTTPandWOPIControl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the frame has been changed
        /// </summary>
        public bool bDirty { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the frame is read-only
        /// </summary>
        public bool bReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the Session object to pull frame data from Fiddler.
        /// </summary>
        //internal Session session { get; set; }

        /// <summary>
        /// Gets or sets the raw bytes from the frame
        /// </summary>
        private byte[] rawBody { get; set; }

        /// <summary>
        /// Gets or sets the base HTTP headers assigned by the request or response
        /// </summary>
        //public HTTPHeaders BaseHeaders { get; set; }

        /// <summary>
        /// Gets or sets the FSSHTTPBytes.
        /// </summary>
        public List<byte[]> FSSHTTPBBytes { get; set; }

        /// <summary>
        /// Gets whether the message is ONESTORE protocol message
        /// </summary>
        public static bool IsOneStore;

        /// <summary>
        /// Encrypted Object Group ID or Object ID List in ONESTORE protocol message
        /// </summary>
        public static List<ExtendedGUID> encryptedObjectGroupIDList = new List<ExtendedGUID>();

        /// <summary>
        /// Bool value indicate wether errorCode in FSSHTTP response is duplicate
        /// </summary>
        public bool isErrorCodeDuplicate;

        /// <summary>
        /// Boolean value to check whether next frame is editors table element
        /// </summary>
        public static bool isNextEditorTable;


        /// <summary>
        /// Called by Fiddler to determine how confident this inspector is that it can
        /// decode the data.  This is only called when the user hits enter or double-
        /// clicks a session.  
        /// If we score the highest out of the other inspectors, Fiddler will open this
        /// inspector's tab and then call AssignSession.
        /// </summary>
        /// <param name="oS">the session object passed by Fiddler</param>
        /// <returns>Int between 0-100 with 100 being the most confident</returns>
        //public override int ScoreForSession(Session oS)
        //{
        //    if (null == this.session)
        //    {
        //        this.session = oS;
        //    }

        //    if (null == this.BaseHeaders)
        //    {
        //        if (this is IRequestInspector2)
        //        {
        //            this.BaseHeaders = this.session.oRequest.headers;
        //        }
        //        else
        //        {
        //            this.BaseHeaders = this.session.oResponse.headers;
        //        }
        //    }

        //    if (this.IsFSSHTTP || this.IsWOPI)
        //    {
        //        return 100;
        //    }
        //    else
        //    {
        //        return 0;
        //    }
        //}

        /// <summary>
        /// This is called every time this inspector is shown
        /// </summary>
        /// <param name="oS">Session object passed by Fiddler</param>
        //public override void AssignSession(Session oS)
        //{
        //    this.session = oS;
        //    base.AssignSession(oS);
        //}

        /// <summary>
        /// Gets or sets the body byte[], called by Fiddler with session byte[]
        /// </summary>
        //public byte[] body
        //{
        //    get
        //    {
        //        return this.rawBody;
        //    }
        //    set
        //    {
        //        this.rawBody = value;
        //        this.UpdateView();
        //    }
        //}

        /// <summary>
        /// Parse the HTTP payload to FSSHTTP and WOPI message.
        /// </summary>
        /// <param name="responseHeaders">The HTTP response header.</param>
        /// <param name="bytesFromHTTP">The raw data from HTTP layer.</param>
        /// <param name="direction">The direction of the traffic.</param>
        /// <returns>The object parsed result</returns>
        //public object ParseHTTPPayloadForFSSHTTP(HttpHeaders responseHeaders, byte[] bytesFromHTTP, TrafficDirection direction)
        //{
        //    object objectOut = null;
        //    string soapbody = "";
        //    byte[] emptyByte = new byte[0];

        //    if (bytesFromHTTP == null || bytesFromHTTP.Length == 0)
        //    {
        //        return null;
        //    }

        //    try
        //    {
        //        if (direction == TrafficDirection.Out && responseHeaders.Exists("Transfer-Encoding") && responseHeaders["Transfer-Encoding"] == "chunked")
        //        {
        //            bytesFromHTTP = Utilities.GetPaylodFromChunkedBody(bytesFromHTTP);
        //        }

        //        Stream stream = new MemoryStream(bytesFromHTTP);
        //        StreamReader reader = new StreamReader(stream);
        //        string text = reader.ReadToEnd();

        //        Regex SOAPRegex = new Regex(@"\<s:Envelop.*\<\/s:Envelope\>"); // extract envelop from http payload.
        //        if (SOAPRegex.Match(text).Success)
        //        {
        //            XmlDocument doc = new XmlDocument();
        //            soapbody = SOAPRegex.Match(text).Value;

        //            if (direction == TrafficDirection.In)
        //            {
        //                Regex FSSHTTPRequestRegex = new Regex("xsi:type=\"\\w*\"\\s"); // remove xsi:type in xml message. this xsi:type is used for inherit in xmlSerializer. 
        //                string FSSHTTPRequest = FSSHTTPRequestRegex.Replace(soapbody, string.Empty);
        //                MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(FSSHTTPRequest ?? ""));
        //                XmlSerializer serializer = new XmlSerializer(typeof(RequestEnvelope));
        //                RequestEnvelope requestEnvelop = (RequestEnvelope)serializer.Deserialize(ms);
        //                objectOut = requestEnvelop.Body;

        //                // if SubRequestData has fsshttpb messages do parser.
        //                if (requestEnvelop.Body.RequestCollection != null)
        //                {
        //                    TryParseFSSHTTPBRequestMessage(requestEnvelop.Body.RequestCollection.Request, bytesFromHTTP);
        //                }
        //            }
        //            else
        //            {
        //                MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(soapbody ?? ""));
        //                XmlSerializer serializer = new XmlSerializer(typeof(ResponseEnvelope));
        //                ResponseEnvelope responseEnvelop = (ResponseEnvelope)serializer.Deserialize(ms);
        //                objectOut = responseEnvelop.Body;

        //                // if SubResponseData has fsshttpb messages do parser.
        //                if (responseEnvelop.Body.ResponseCollection != null)
        //                {
        //                    TryParseFSSHTTPBResponseMessage(responseEnvelop.Body.ResponseCollection.Response, bytesFromHTTP);
        //                }
        //            }
        //        }
        //        return objectOut;
        //    }
        //    catch (InvalidOperationException e)
        //    {
        //        if (e.InnerException.Message.Contains("ErrorCode") && e.InnerException.StackTrace.Contains("AttributeDuplCheck"))
        //        {
        //            objectOut = soapbody;
        //            isErrorCodeDuplicate = true;
        //        }
        //        else
        //        {
        //            objectOut = e.ToString();
        //        }
        //        return objectOut;
        //    }
        //    catch (Exception ex)
        //    {
        //        objectOut = ex.ToString();
        //        return objectOut;
        //    }
        //}

        /// <summary>
        /// Parse the HTTP payload to WOPI message.
        /// </summary>
        /// <param name="requestHeaders">The HTTP request header.</param>
        /// <param name="responseHeaders">The HTTP response header.</param>
        /// <param name="url">url for a HTTP message.</param>
        /// <param name="bytesFromHTTP">The raw data from HTTP layer.</param>
        /// <param name="direction">The direction of the traffic.</param>
        /// <returns>The object parsed result</returns>
        //public object ParseHTTPPayloadForWOPI(HTTPHeaders requestHeaders, HTTPHeaders responseHeaders, string url, byte[] bytesFromHTTP, out string binaryStructureRopName, TrafficDirection direction)
        //{
        //    object objectOut = null;
        //    string res = "";
        //    binaryStructureRopName = string.Empty;
        //    try
        //    {
        //        if (direction == TrafficDirection.Out && responseHeaders.Exists("Transfer-Encoding") && responseHeaders["Transfer-Encoding"] == "chunked")
        //        {
        //            bytesFromHTTP = Utilities.GetPaylodFromChunkedBody(bytesFromHTTP);
        //        }

        //        Stream stream = new MemoryStream(bytesFromHTTP);
        //        StreamReader reader = new StreamReader(stream);
        //        string text = reader.ReadToEnd();
        //        WOPIOperations operation = GetWOPIOperationName(requestHeaders, url);
        //        if (direction == TrafficDirection.In)
        //        {
        //            switch (operation)
        //            {
        //                case WOPIOperations.PutRelativeFile:
        //                    objectOut = bytesFromHTTP;
        //                    binaryStructureRopName = "PutRelativeFile";
        //                    break;
        //                case WOPIOperations.PutFile:
        //                    objectOut = bytesFromHTTP;
        //                    binaryStructureRopName = "PutFile";
        //                    break;
        //                case WOPIOperations.ExecuteCellStorageRelativeRequest:
        //                case WOPIOperations.ExecuteCellStorageRequest:
        //                    byte[] cellreq = bytesFromHTTP;
        //                    MemoryStream ms;
        //                    string req;
        //                    if (text.Contains("<s:Envelope"))
        //                    {
        //                        if (requestHeaders.Exists("Content-Encoding") && requestHeaders["Content-Encoding"] == "gzip")
        //                        {
        //                            cellreq = Fiddler.Utilities.GzipExpand(cellreq);
        //                            ms = new MemoryStream(cellreq);
        //                        }
        //                        else
        //                        {
        //                            ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text ?? ""));
        //                        }
        //                        XmlSerializer serializer = new XmlSerializer(typeof(RequestEnvelope));
        //                        RequestEnvelope Envelop = (RequestEnvelope)serializer.Deserialize(ms);
        //                        objectOut = Envelop.Body;

        //                        if (Envelop.Body.RequestCollection != null)
        //                        {
        //                            TryParseFSSHTTPBRequestMessage(Envelop.Body.RequestCollection.Request, bytesFromHTTP);
        //                        }
        //                        break;
        //                    }
        //                    else
        //                    {
        //                        if (requestHeaders.Exists("Content-Encoding") && requestHeaders["Content-Encoding"] == "gzip")
        //                        {
        //                            cellreq = Fiddler.Utilities.GzipExpand(cellreq);
        //                            string req_sub = System.Text.Encoding.UTF8.GetString(cellreq);
        //                            req = string.Format("{0}{1}{2}", @"<Body>", req_sub, "</Body>");
        //                        }
        //                        else
        //                        {
        //                            req = string.Format("{0}{1}{2}", @"<Body>", text, "</Body>");
        //                        }
        //                        ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(req ?? ""));
        //                        XmlSerializer serializer = new XmlSerializer(typeof(RequestEnvelopeBody));
        //                        RequestEnvelopeBody body = (RequestEnvelopeBody)serializer.Deserialize(ms);
        //                        objectOut = body;

        //                        if (body.RequestCollection != null)
        //                        {
        //                            TryParseFSSHTTPBRequestMessage(body.RequestCollection.Request, bytesFromHTTP);
        //                        }
        //                        break;
        //                    }

        //                case WOPIOperations.PutUserInfo:
        //                    objectOut = text;
        //                    break;
        //                case WOPIOperations.Discovery:
        //                case WOPIOperations.CheckFileInfo:
        //                case WOPIOperations.Lock:
        //                case WOPIOperations.RefreshLock:
        //                case WOPIOperations.RevokeRestrictedLink:
        //                case WOPIOperations.Unlock:
        //                case WOPIOperations.UnlockAndRelock:
        //                case WOPIOperations.GetLock:
        //                case WOPIOperations.DeleteFile:
        //                case WOPIOperations.ReadSecureStore:
        //                case WOPIOperations.RenameFile:
        //                case WOPIOperations.GetRestrictedLink:
        //                case WOPIOperations.CheckFolderInfo:
        //                case WOPIOperations.GetFile:
        //                case WOPIOperations.EnumerateChildren:
        //                    objectOut = string.Format("{0} operation's request body is null", operation.ToString());
        //                    break;
        //                default:
        //                    throw new Exception("The WOPI operations type is not right.");
        //            }
        //        }
        //        else
        //        {
        //            string status = this.session.ResponseHeaders.HTTPResponseStatus.Replace(" " + this.session.ResponseHeaders.StatusDescription, string.Empty);
        //            if (Convert.ToUInt32(status) != 200)// the status is not success
        //                return null;

        //            ResponseBodyBase responseBody = new ResponseBodyBase();
        //            switch (operation)
        //            {
        //                case WOPIOperations.Discovery:
        //                    MemoryStream discoveryms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text ?? ""));
        //                    XmlSerializer discoverySerializer = new XmlSerializer(typeof(wopidiscovery));
        //                    wopidiscovery discoveryres = (wopidiscovery)discoverySerializer.Deserialize(discoveryms);
        //                    objectOut = discoveryres;
        //                    break;
        //                case WOPIOperations.CheckFileInfo:
        //                    objectOut = WOPISerilizer.JsonToObject<CheckFileInfo>(text);
        //                    break;
        //                case WOPIOperations.CheckFolderInfo:
        //                    objectOut = WOPISerilizer.JsonToObject<CheckFolderInfo>(text);
        //                    break;
        //                case WOPIOperations.PutRelativeFile:
        //                    objectOut = WOPISerilizer.JsonToObject<PutRelativeFile>(text);
        //                    break;
        //                case WOPIOperations.ReadSecureStore:
        //                    objectOut = WOPISerilizer.JsonToObject<ReadSecureStore>(text);
        //                    break;
        //                case WOPIOperations.EnumerateChildren:
        //                    objectOut = WOPISerilizer.JsonToObject<EnumerateChildren>(text);
        //                    break;
        //                case WOPIOperations.RenameFile:
        //                    objectOut = WOPISerilizer.JsonToObject<RenameFile>(text);
        //                    break;
        //                case WOPIOperations.ExecuteCellStorageRelativeRequest:
        //                case WOPIOperations.ExecuteCellStorageRequest:
        //                    {
        //                        byte[] cellres = bytesFromHTTP;
        //                        MemoryStream ms;
        //                        if (responseHeaders.Exists("Content-Encoding") && responseHeaders["Content-Encoding"] == "gzip")
        //                        {
        //                            cellres = Fiddler.Utilities.GzipExpand(cellres);
        //                            string res_sub = System.Text.Encoding.UTF8.GetString(cellres);
        //                            res = string.Format("{0}{1}{2}", @"<Body>", res_sub, "</Body>");
        //                        }
        //                        else
        //                        {
        //                            res = string.Format("{0}{1}{2}", @"<Body>", text, "</Body>");
        //                        }

        //                        try
        //                        {
        //                            ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(res ?? ""));
        //                            XmlSerializer serializer = new XmlSerializer(typeof(ResponseEnvelopeBody));
        //                            ResponseEnvelopeBody body = (ResponseEnvelopeBody)serializer.Deserialize(ms);
        //                            objectOut = body;

        //                            // if SubResponseData has fsshttpb messages do parser.
        //                            if (body.ResponseCollection != null)
        //                            {
        //                                TryParseFSSHTTPBResponseMessage(body.ResponseCollection.Response, bytesFromHTTP);
        //                            }
        //                        }
        //                        catch
        //                        {
        //                            Regex SOAPRegex = new Regex(@"\<s:Envelop.*\<\/s:Envelope\>"); // extract envelop from http payload.
        //                            if (SOAPRegex.Match(res).Success)
        //                            {
        //                                XmlDocument doc = new XmlDocument();
        //                                string soapbody = SOAPRegex.Match(res).Value;

        //                                MemoryStream memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(soapbody ?? ""));
        //                                XmlSerializer serializer = new XmlSerializer(typeof(ResponseEnvelope));
        //                                ResponseEnvelope responseEnvelop = (ResponseEnvelope)serializer.Deserialize(memoryStream);
        //                                objectOut = responseEnvelop.Body;

        //                                // if SubResponseData has fsshttpb messages do parser.
        //                                if (responseEnvelop.Body.ResponseCollection != null)
        //                                {
        //                                    TryParseFSSHTTPBResponseMessage(responseEnvelop.Body.ResponseCollection.Response, bytesFromHTTP);
        //                                }
        //                            }
        //                        }
        //                        break;
        //                    }
        //                case WOPIOperations.GetFile:
        //                    objectOut = bytesFromHTTP;
        //                    binaryStructureRopName = "GetFile";
        //                    break;
        //                case WOPIOperations.DeleteFile:
        //                case WOPIOperations.Lock:
        //                case WOPIOperations.GetRestrictedLink:
        //                case WOPIOperations.PutFile:
        //                case WOPIOperations.RefreshLock:
        //                case WOPIOperations.RevokeRestrictedLink:
        //                case WOPIOperations.Unlock:
        //                case WOPIOperations.UnlockAndRelock:
        //                case WOPIOperations.GetLock:
        //                case WOPIOperations.PutUserInfo:
        //                    objectOut = string.Format("{0} operation's response body is null", operation.ToString());
        //                    break;
        //                default:
        //                    throw new Exception("The WOPI operations type is not right.");
        //            }
        //        }
        //        return objectOut;
        //    }
        //    catch (InvalidOperationException e)
        //    {
        //        if (e.InnerException.Message.Contains("ErrorCode") && e.InnerException.StackTrace.Contains("AttributeDuplCheck"))
        //        {
        //            objectOut = res;
        //            isErrorCodeDuplicate = true;
        //        }
        //        else
        //        {
        //            objectOut = e.ToString();
        //        }
        //        return objectOut;
        //    }
        //    catch (Exception ex)
        //    {
        //        objectOut = ex.ToString();
        //        return objectOut;
        //    }
        //}

        /// <summary>
        /// Parse the HTTP payload to FSSHTTPB Request message.
        /// </summary>
        /// <param name="Requests">Array of Request that is part of a cell storage service request.</param>
        /// <param name="bytesFromHTTP">The raw data from HTTP layer.</param>
        public void TryParseFSSHTTPBRequestMessage(Request[] Requests, byte[] bytesFromHTTP)
        {
            if (Requests == null)
                return;

            byte[][] includeTexts = GetOctetsBinaryForXOP(bytesFromHTTP, true).ToArray();
            int index = 0;

            foreach (Request req in Requests)
            {
                if (req.SubRequest != null && req.SubRequest.Length > 0)
                {
                    foreach (SubRequestElementGenericType subreq in req.SubRequest)
                    {
                        if (subreq.SubRequestData != null)
                        {
                            if (subreq.SubRequestData.Text != null && subreq.SubRequestData.Text.Length > 0)
                            {
                                string textValue = subreq.SubRequestData.Text[0];
                                byte[] FSSHTTPBTextBytes = Convert.FromBase64String(textValue);

                                if (!IsFSSHTTPBStart(FSSHTTPBTextBytes))
                                    return;

                                FsshttpbRequest Fsshttpbreq = (FsshttpbRequest)ParseFSSHTTPBBytes(FSSHTTPBTextBytes, TrafficDirection.In);
                                subreq.SubRequestData.TextObject = Fsshttpbreq;
                                FSSHTTPBBytes.Add(FSSHTTPBTextBytes);
                            }

                            if (subreq.SubRequestData.Include != null)
                            {
                                byte[] FSSHTTPBIncludeBytes = includeTexts[index++];
                                FsshttpbRequest Fsshttpbreq = (FsshttpbRequest)ParseFSSHTTPBBytes(FSSHTTPBIncludeBytes, TrafficDirection.In);
                                subreq.SubRequestData.IncludeObject = Fsshttpbreq;
                                FSSHTTPBBytes.Add(FSSHTTPBIncludeBytes);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Parse the HTTP payload to FSSHTTPB Response message.
        /// </summary>
        /// <param name="Responses">Array of Response element that is part of a cell storage service response.</param>
        /// <param name="bytesFromHTTP">The raw data from HTTP layer.</param>
        public void TryParseFSSHTTPBResponseMessage(Response[] Responses, byte[] bytesFromHTTP)
        {
            if (Responses == null)
                return;

            byte[][] includeTexts = GetOctetsBinaryForXOP(bytesFromHTTP, false).ToArray();
            int index = 0;

            foreach (Response res in Responses)
            {
                // If response is for ONESTORE,set FSSHTTPandWOPIInspector.IsOneStore ture.
                if (res.Url != null)
                {
                    if (res.Url.EndsWith(".one") || res.Url.EndsWith(".onetoc2"))
                    {
                        FSSHTTPandWOPIInspector.IsOneStore = true;
                    }
                    else
                    {
                        FSSHTTPandWOPIInspector.IsOneStore = false;
                    }
                }

                if (res.SubResponse != null && res.SubResponse.Length > 0)
                {
                    foreach (SubResponseElementGenericType subres in res.SubResponse)
                    {
                        if (subres.SubResponseData == null)
                            continue;

                        if (subres.SubResponseData.Text != null && subres.SubResponseData.Text.Length > 0)
                        {
                            string textValue = subres.SubResponseData.Text[0];
                            byte[] FSSHTTPBTextBytes = Convert.FromBase64String(textValue);
                            FsshttpbResponse Fsshttpbres = (FsshttpbResponse)ParseFSSHTTPBBytes(FSSHTTPBTextBytes, TrafficDirection.Out);
                            subres.SubResponseData.TextObject = Fsshttpbres;
                            FSSHTTPBBytes.Add(FSSHTTPBTextBytes);
                        }

                        if (subres.SubResponseData.Include != null)
                        {
                            byte[] FSSHTTPBIncludeBytes = includeTexts[index++];
                            FsshttpbResponse Fsshttpbres = (FsshttpbResponse)ParseFSSHTTPBBytes(FSSHTTPBIncludeBytes, TrafficDirection.Out);
                            subres.SubResponseData.IncludeObject = Fsshttpbres;
                            FSSHTTPBBytes.Add(FSSHTTPBIncludeBytes);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Parse the FSSHTTPB Bytes.
        /// </summary>
        /// <param name="FSSHTTPBbytes">The raw date contains FSSHTTPB message.</param>
        /// <param name="direction">The direction of the traffic.</param>
        /// <returns>The object parsed result</returns>
        public object ParseFSSHTTPBBytes(byte[] FSSHTTPBbytes, TrafficDirection direction)
        {
            object objectOut = null;
            byte[] emptyByte = new byte[0];
            if (FSSHTTPBbytes == null || FSSHTTPBbytes.Length == 0)
            {
                return null;
            }

            try
            {
                if (direction == TrafficDirection.In)
                {
                    FsshttpbRequest FsshttpbReq = new FsshttpbRequest();
                    MemoryStream s = new MemoryStream(FSSHTTPBbytes);
                    FsshttpbReq.Parse(s);
                    objectOut = FsshttpbReq;
                }
                else
                {
                    FsshttpbResponse FsshttpbRes = new FsshttpbResponse();
                    MemoryStream s = new MemoryStream(FSSHTTPBbytes);
                    FsshttpbRes.Parse(s);
                    objectOut = FsshttpbRes;
                }

                return objectOut;

            }
            catch (Exception ex)
            {
                objectOut = ex.ToString();
                return objectOut;
            }
        }

        /// <summary>
        /// Enum for traffic direction
        /// </summary>
        public enum TrafficDirection
        {
            In,
            Out
        }

        #region Help methods
        /// <summary>
        /// Concert byte array to hex string
        /// </summary>
        /// <param name="ba">The byte array used to convert</param>
        /// <returns>Hex string value</returns>
        public string BytearrayToString(byte[] ba)
        {
            StringBuilder st = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
            {
                st.AppendFormat("{0:x2}", b);
            }
            return st.ToString();
        }

        /// <summary>
        /// Get the string of octets binary in XOP package
        /// </summary>
        /// <param name="bytesFromHTTP">The raw data from HTTP layer</param>
        /// <param name="IncludeTextLength">Out the length of octets binary</param>
        /// <returns>A int value indicate the position of the octets binary in the bytesFromHTTP</returns>
        public List<byte[]> GetOctetsBinaryForXOP(byte[] bytesFromHTTP, bool isRequest)
        {
            string HexString = BytearrayToString(bytesFromHTTP);
            Regex MIMEBoundaryRegex;
            Regex octetsBinaryRegex;
            bool bounaryContainUrn = true;
            if (isRequest)
            {
                MIMEBoundaryRegex = new Regex(@"2d2d75726e3a75756964");// MIME bounary is --urn:uuid
                octetsBinaryRegex = new Regex(@"2d2d75726e3a75756964([\s\S]*?)(?=2d2d75726e3a75756964)");// This regex is used to get substring between two --urn:uuid.
                if (MIMEBoundaryRegex.Matches(HexString).Count == 0)
                {
                    MIMEBoundaryRegex = new Regex(@"2d2d75756964");// MIME bounary is --uuid
                    octetsBinaryRegex = new Regex(@"2d2d75756964([\s\S]*?)(?=2d2d75756964)");// This regex is used to get substring between two --uuid.
                    bounaryContainUrn = false;
                }
            }
            else
            {
                MIMEBoundaryRegex = new Regex(@"2d2d75756964");// MIME bounary is --uuid
                octetsBinaryRegex = new Regex(@"2d2d75756964([\s\S]*?)(?=2d2d75756964)");// This regex is used to get substring between two --uuid.
            }

            Regex IncludeRegex = new Regex(@"2d2d[\s\S]*0d0a0d0a"); // This regex is used to get the Include text(octets Binary minus include Header)
            List<byte[]> IncludeTexts = new List<byte[]>();
            if (MIMEBoundaryRegex.Matches(HexString).Count >= 3)
            {
                // remove first MIME bounary from HexString
                string firstMIMEBoundary = MIMEBoundaryRegex.Match(HexString).Value;
                HexString = MIMEBoundaryRegex.Replace(HexString, string.Empty, 1);
                int HistoryPosition = 0;
                if (isRequest && bounaryContainUrn)
                {
                    HistoryPosition = 10; // 10 is the length of first --urn:uuid in bytesFromHTTP, it has been removed in HexString
                }
                else
                {
                    HistoryPosition = 6; // 6 is the length of first --uuid in bytesFromHTTP, it has been removed in HexString
                }

                int MIMEBoundaryEaroIndex = 0;
                int LastIncludeLength = 0;
                // Get all include text binary
                while (octetsBinaryRegex.Matches(HexString).Count > 0)
                {
                    string octetsBinary = octetsBinaryRegex.Match(HexString).Value;
                    string includeHeader = IncludeRegex.Match(octetsBinary).Value;
                    string includeText = IncludeRegex.Replace(octetsBinary, string.Empty, 1);
                    int ThisIncludePosition = HexString.IndexOf(includeText) / 2;// One char in byte array as a string 
                    int ThisIncludeLength = includeText.Length / 2 - 2; // (-2) because behinde every include text there is a 0D0A

                    byte[] includeByte = new byte[ThisIncludeLength];
                    if (MIMEBoundaryEaroIndex == 0)
                    {
                        HistoryPosition += ThisIncludePosition;

                    }
                    else
                    {
                        HistoryPosition += (LastIncludeLength + includeHeader.Length / 2 + 2);// (+2) because behinde every include text there is a 0D0A
                    }
                    LastIncludeLength = ThisIncludeLength;
                    Array.Copy(bytesFromHTTP, HistoryPosition, includeByte, 0, ThisIncludeLength);
                    IncludeTexts.Add(includeByte);
                    HexString = octetsBinaryRegex.Replace(HexString, string.Empty, 1);
                    MIMEBoundaryEaroIndex++;
                }
                return IncludeTexts;
            }
            return IncludeTexts;
        }

        /// <summary>
        /// Check if the start point is FSSHTTPB bits
        /// </summary>
        /// <param name="payload"></param>
        /// <returns>bool value indicate if the payload is fsshttpb or not</returns>
        public bool IsFSSHTTPBStart(byte[] payload)
        {
            if (payload == null || payload.Length < 4)
                return false;

            if ((payload[0] == 0x0C && payload[1] == 0x00 && payload[2] == 0x0B && payload[3] == 0x00)
                || (payload[0] == 0x0D && payload[1] == 0x00 && payload[2] == 0x0B && payload[3] == 0x00)
                || (payload[0] == 0x0E && payload[1] == 0x00 && payload[2] == 0x0B && payload[3] == 0x00))
            {
                return true;
            }
            return false;
        }
        #endregion
    }
}