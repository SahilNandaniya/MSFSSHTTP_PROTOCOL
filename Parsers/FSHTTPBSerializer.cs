namespace MSFSSHTTP.Parsers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Serializer for MS-FSSHTTPB primitives and structures.
    /// Bit-perfect reverse of the parser in FSSHTTPB.cs.
    /// </summary>
    public static class FSSHTTPBSerializer
    {
        // ───────────────────────────────────────────────
        // 2.2.1.1  Compact Unsigned 64-bit Integer
        // ───────────────────────────────────────────────

        /// <summary>
        /// Serialize a CompactUnsigned64bitInteger to the writer.
        /// Symmetric to CompactUnsigned64bitInteger.TryParse().
        /// </summary>
        public static void Serialize(this CompactUnsigned64bitInteger obj, BinaryWriter writer)
        {
            if (obj is CompactUintZero z)
            {
                z.Serialize(writer);
            }
            else if (obj is CompactUint7bitvalues v7)
            {
                v7.Serialize(writer);
            }
            else if (obj is CompactUint14bitvalues v14)
            {
                v14.Serialize(writer);
            }
            else if (obj is CompactUint21bitvalues v21)
            {
                v21.Serialize(writer);
            }
            else if (obj is CompactUint28bitvalues v28)
            {
                v28.Serialize(writer);
            }
            else if (obj is CompactUint35bitvalues v35)
            {
                v35.Serialize(writer);
            }
            else if (obj is CompactUint42bitvalues v42)
            {
                v42.Serialize(writer);
            }
            else if (obj is CompactUint49bitvalues v49)
            {
                v49.Serialize(writer);
            }
            else if (obj is CompactUint64bitvalues v64)
            {
                v64.Serialize(writer);
            }
            else
            {
                throw new InvalidOperationException("Unknown CompactUnsigned64bitInteger subtype.");
            }
        }

        /// <summary>
        /// Create and serialize a CompactUnsigned64bitInteger from a raw ulong value,
        /// choosing the smallest encoding that fits.
        /// </summary>
        public static CompactUnsigned64bitInteger CreateCompactUint64(ulong value)
        {
            if (value == 0)
            {
                return new CompactUintZero { Uint = 0 };
            }
            else if (value <= 0x7F)
            {
                return new CompactUint7bitvalues { A = 0x01, Uint = (byte)value };
            }
            else if (value <= 0x3FFF)
            {
                return new CompactUint14bitvalues { A = 0x02, Uint = (ushort)value };
            }
            else if (value <= 0x1FFFFF)
            {
                return new CompactUint21bitvalues { A = 0x04, Uint = (uint)value };
            }
            else if (value <= 0x0FFFFFFF)
            {
                return new CompactUint28bitvalues { A = 0x08, Uint = (uint)value };
            }
            else if (value <= 0x7FFFFFFFF)
            {
                return new CompactUint35bitvalues { A = 0x10, Uint = value };
            }
            else if (value <= 0x3FFFFFFFFFF)
            {
                return new CompactUint42bitvalues { A = 0x20, Uint = value };
            }
            else if (value <= 0x1FFFFFFFFFFFF)
            {
                return new CompactUint49bitvalues { A = 0x40, Uint = value };
            }
            else
            {
                return new CompactUint64bitvalues { A = 0x80, Uint = value };
            }
        }

        /// <summary>
        /// Serialize CompactUintZero – writes 1 byte (0x00).
        /// Parser: ReadByte() → Uint = 0x00.
        /// </summary>
        public static void Serialize(this CompactUintZero obj, BinaryWriter writer)
        {
            writer.Write(obj.Uint); // 1 byte: 0x00
        }

        /// <summary>
        /// Serialize CompactUint7bitvalues – writes 1 byte.
        /// Parser: ReadByte() → A=bits[0..0], Uint=bits[1..7].
        /// </summary>
        public static void Serialize(this CompactUint7bitvalues obj, BinaryWriter writer)
        {
            int temp = (obj.A & 0x01) | ((obj.Uint & 0x7F) << 1);
            writer.Write((byte)temp);
        }

        /// <summary>
        /// Serialize CompactUint14bitvalues – writes 2 bytes (ushort LE).
        /// Parser: ReadUshort() → A=bits[0..1], Uint=bits[2..15].
        /// </summary>
        public static void Serialize(this CompactUint14bitvalues obj, BinaryWriter writer)
        {
            int temp = (obj.A & 0x03) | ((obj.Uint & 0x3FFF) << 2);
            writer.Write((ushort)temp);
        }

        /// <summary>
        /// Serialize CompactUint21bitvalues – writes 3 bytes.
        /// Parser: Read3Bytes() → A=bits[0..2], Uint=bits[3..23].
        /// </summary>
        public static void Serialize(this CompactUint21bitvalues obj, BinaryWriter writer)
        {
            int temp = (obj.A & 0x07) | ((int)(obj.Uint & 0x1FFFFF) << 3);
            writer.Write((byte)(temp & 0xFF));
            writer.Write((byte)((temp >> 8) & 0xFF));
            writer.Write((byte)((temp >> 16) & 0xFF));
        }

        /// <summary>
        /// Serialize CompactUint28bitvalues – writes 4 bytes (uint LE).
        /// Parser: ReadUint() → A=bits[0..3], Uint=bits[4..31].
        /// </summary>
        public static void Serialize(this CompactUint28bitvalues obj, BinaryWriter writer)
        {
            uint temp = (uint)(obj.A & 0x0F) | ((obj.Uint & 0x0FFFFFFF) << 4);
            writer.Write(temp);
        }

        /// <summary>
        /// Serialize CompactUint35bitvalues – writes 5 bytes.
        /// Parser: Read5Bytes() → A=bits[0..4], Uint=bits[5..39].
        /// </summary>
        public static void Serialize(this CompactUint35bitvalues obj, BinaryWriter writer)
        {
            long temp = (long)(obj.A & 0x1F) | ((long)(obj.Uint & 0x7FFFFFFFF) << 5);
            writer.Write((byte)(temp & 0xFF));
            writer.Write((byte)((temp >> 8) & 0xFF));
            writer.Write((byte)((temp >> 16) & 0xFF));
            writer.Write((byte)((temp >> 24) & 0xFF));
            writer.Write((byte)((temp >> 32) & 0xFF));
        }

        /// <summary>
        /// Serialize CompactUint42bitvalues – writes 6 bytes.
        /// Parser: Read6Bytes() → A=bits[0..5], Uint=bits[6..47].
        /// </summary>
        public static void Serialize(this CompactUint42bitvalues obj, BinaryWriter writer)
        {
            long temp = (long)(obj.A & 0x3F) | ((long)(obj.Uint & 0x3FFFFFFFFFF) << 6);
            writer.Write((byte)(temp & 0xFF));
            writer.Write((byte)((temp >> 8) & 0xFF));
            writer.Write((byte)((temp >> 16) & 0xFF));
            writer.Write((byte)((temp >> 24) & 0xFF));
            writer.Write((byte)((temp >> 32) & 0xFF));
            writer.Write((byte)((temp >> 40) & 0xFF));
        }

        /// <summary>
        /// Serialize CompactUint49bitvalues – writes 7 bytes.
        /// Parser: Read7Bytes() → A=bits[0..6], Uint=bits[7..55].
        /// </summary>
        public static void Serialize(this CompactUint49bitvalues obj, BinaryWriter writer)
        {
            long temp = (long)(obj.A & 0x7F) | ((long)(obj.Uint & 0x1FFFFFFFFFFFF) << 7);
            writer.Write((byte)(temp & 0xFF));
            writer.Write((byte)((temp >> 8) & 0xFF));
            writer.Write((byte)((temp >> 16) & 0xFF));
            writer.Write((byte)((temp >> 24) & 0xFF));
            writer.Write((byte)((temp >> 32) & 0xFF));
            writer.Write((byte)((temp >> 40) & 0xFF));
            writer.Write((byte)((temp >> 48) & 0xFF));
        }

        /// <summary>
        /// Serialize CompactUint64bitvalues – writes 1+8 = 9 bytes.
        /// Parser: ReadByte() → A=0x80, ReadUlong() → Uint.
        /// </summary>
        public static void Serialize(this CompactUint64bitvalues obj, BinaryWriter writer)
        {
            writer.Write(obj.A); // 0x80
            writer.Write(obj.Uint); // 8 bytes LE
        }

        // ───────────────────────────────────────────────
        // 2.2.1.2  File Chunk Reference
        // ───────────────────────────────────────────────

        public static void Serialize(this FileChunkReference obj, BinaryWriter writer)
        {
            obj.Start.Serialize(writer);
            obj.Length.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.3  Binary Item
        // ───────────────────────────────────────────────

        public static void Serialize(this BinaryItem obj, BinaryWriter writer)
        {
            obj.Length.Serialize(writer);
            if (obj.Content != null && obj.Content.Length > 0)
            {
                writer.Write(obj.Content);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.1.4  String Item
        // ───────────────────────────────────────────────

        public static void Serialize(this StringItem obj, BinaryWriter writer)
        {
            obj.Count.Serialize(writer);
            if (obj.Content != null)
            {
                // Parser reads Count Unicode chars via ReadString(Encoding.Unicode, "", Count)
                byte[] encoded = Encoding.Unicode.GetBytes(obj.Content);
                writer.Write(encoded);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.1.14  String Item Array
        // ───────────────────────────────────────────────

        public static void Serialize(this StringItemArray obj, BinaryWriter writer)
        {
            obj.Count.Serialize(writer);
            if (obj.Content != null)
            {
                for (int i = 0; i < obj.Content.Length; i++)
                {
                    obj.Content[i].Serialize(writer);
                }
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.1.5  Stream Object Headers
        // ───────────────────────────────────────────────

        /// <summary>
        /// Serialize a StreamObjectHeader (dispatches to concrete subtypes).
        /// </summary>
        public static void Serialize(this StreamObjectHeader obj, BinaryWriter writer)
        {
            if (obj is bit16StreamObjectHeaderStart h16s)
            {
                h16s.Serialize(writer);
            }
            else if (obj is bit32StreamObjectHeaderStart h32s)
            {
                h32s.Serialize(writer);
            }
            else if (obj is bit8StreamObjectHeaderEnd h8e)
            {
                h8e.Serialize(writer);
            }
            else if (obj is bit16StreamObjectHeaderEnd h16e)
            {
                h16e.Serialize(writer);
            }
            else
            {
                throw new InvalidOperationException("Unknown StreamObjectHeader subtype.");
            }
        }

        /// <summary>
        /// 2.2.1.5.1  16-bit Stream Object Header Start – writes 2 bytes (ushort LE).
        /// Parser: ReadUshort() → A=bits[0..1], B=bits[2], Type=bits[3..8], Length=bits[9..15].
        /// </summary>
        public static void Serialize(this bit16StreamObjectHeaderStart obj, BinaryWriter writer)
        {
            int temp = (obj.A & 0x03)
                     | ((obj.B & 0x01) << 2)
                     | (((int)obj.Type & 0x3F) << 3)
                     | ((obj.Length & 0x7F) << 9);
            writer.Write((ushort)temp);
        }

        /// <summary>
        /// 2.2.1.5.2  32-bit Stream Object Header Start – writes 4 bytes (uint LE), optionally followed by LargeLength.
        /// Parser: ReadUint() → A=bits[0..1], B=bits[2], Type=bits[3..16], Length=bits[17..31].
        /// </summary>
        public static void Serialize(this bit32StreamObjectHeaderStart obj, BinaryWriter writer)
        {
            uint temp = (uint)(obj.A & 0x03)
                      | ((uint)(obj.B & 0x01) << 2)
                      | ((uint)((int)obj.Type & 0x3FFF) << 3)
                      | ((uint)(obj.Length & 0x7FFF) << 17);
            writer.Write(temp);

            if (obj.Length == 32767 && obj.LargeLength != null)
            {
                obj.LargeLength.Serialize(writer);
            }
        }

        /// <summary>
        /// 2.2.1.5.3  8-bit Stream Object Header End – writes 1 byte.
        /// Parser: ReadByte() → A=bits[0..1], Type=bits[2..7].
        /// </summary>
        public static void Serialize(this bit8StreamObjectHeaderEnd obj, BinaryWriter writer)
        {
            int temp = (obj.A & 0x03) | (((int)obj.Type & 0x3F) << 2);
            writer.Write((byte)temp);
        }

        /// <summary>
        /// 2.2.1.5.4  16-bit Stream Object Header End – writes 2 bytes (ushort LE).
        /// Parser: ReadUshort() → A=bits[0..1], Type=bits[2..15].
        /// </summary>
        public static void Serialize(this bit16StreamObjectHeaderEnd obj, BinaryWriter writer)
        {
            int temp = (obj.A & 0x03) | (((int)obj.Type & 0x3FFF) << 2);
            writer.Write((ushort)temp);
        }

        // ───────────────────────────────────────────────
        // Helper: create Stream Object Headers from type enums
        // ───────────────────────────────────────────────

        public static bit16StreamObjectHeaderStart Create16BitStreamObjectHeaderStart(StreamObjectTypeHeaderStart type, byte length, byte compound = 0)
        {
            return new bit16StreamObjectHeaderStart
            {
                A = 0x00, // 2 bits: 00 = 16-bit start
                B = compound,
                Type = type,
                Length = length
            };
        }

        public static bit32StreamObjectHeaderStart Create32BitStreamObjectHeaderStart(StreamObjectTypeHeaderStart type, short length, byte compound = 0, CompactUnsigned64bitInteger largeLength = null)
        {
            return new bit32StreamObjectHeaderStart
            {
                A = 0x02, // 2 bits: 10 = 32-bit start
                B = compound,
                Type = type,
                Length = length,
                LargeLength = largeLength
            };
        }

        public static bit8StreamObjectHeaderEnd Create8BitStreamObjectHeaderEnd(StreamObjectTypeHeaderEnd type)
        {
            return new bit8StreamObjectHeaderEnd
            {
                A = 0x01, // 2 bits: 01 = 8-bit end
                Type = type
            };
        }

        public static bit16StreamObjectHeaderEnd Create16BitStreamObjectHeaderEnd(StreamObjectTypeHeaderEnd type)
        {
            return new bit16StreamObjectHeaderEnd
            {
                A = 0x03, // 2 bits: 11 = 16-bit end
                Type = type
            };
        }

        // ───────────────────────────────────────────────
        // 2.2.1.7  Extended GUID
        // ───────────────────────────────────────────────

        public static void Serialize(this ExtendedGUID obj, BinaryWriter writer)
        {
            if (obj is ExtendedGUIDNullValue n)
            {
                n.Serialize(writer);
            }
            else if (obj is ExtendedGUID5BitUintValue v5)
            {
                v5.Serialize(writer);
            }
            else if (obj is ExtendedGUID10BitUintValue v10)
            {
                v10.Serialize(writer);
            }
            else if (obj is ExtendedGUID17BitUintValue v17)
            {
                v17.Serialize(writer);
            }
            else if (obj is ExtendedGUID32BitUintValue v32)
            {
                v32.Serialize(writer);
            }
            else
            {
                throw new InvalidOperationException("Unknown ExtendedGUID subtype.");
            }
        }

        /// <summary>
        /// Parser: ReadByte() → Type = 0x00.
        /// </summary>
        public static void Serialize(this ExtendedGUIDNullValue obj, BinaryWriter writer)
        {
            writer.Write(obj.Type); // 0x00
        }

        /// <summary>
        /// Parser: ReadByte() → Type=bits[0..2], Value=bits[3..7]; ReadGuid().
        /// </summary>
        public static void Serialize(this ExtendedGUID5BitUintValue obj, BinaryWriter writer)
        {
            byte temp = (byte)((obj.Type & 0x07) | ((obj.Value & 0x1F) << 3));
            writer.Write(temp);
            writer.Write(obj.GUID.ToByteArray());
        }

        /// <summary>
        /// Parser: ReadUshort() → Type=bits[0..5], Value=bits[6..15]; ReadGuid().
        /// </summary>
        public static void Serialize(this ExtendedGUID10BitUintValue obj, BinaryWriter writer)
        {
            ushort temp = (ushort)((obj.Type & 0x3F) | ((obj.Value & 0x03FF) << 6));
            writer.Write(temp);
            writer.Write(obj.GUID.ToByteArray());
        }

        /// <summary>
        /// Parser: Read3Bytes() → Type=bits[0..6], Value=bits[7..23]; ReadGuid().
        /// </summary>
        public static void Serialize(this ExtendedGUID17BitUintValue obj, BinaryWriter writer)
        {
            int temp = (int)(obj.Type & 0x7F) | (int)((obj.Value & 0x1FFFF) << 7);
            writer.Write((byte)(temp & 0xFF));
            writer.Write((byte)((temp >> 8) & 0xFF));
            writer.Write((byte)((temp >> 16) & 0xFF));
            writer.Write(obj.GUID.ToByteArray());
        }

        /// <summary>
        /// Parser: ReadByte() → Type=0x80; ReadUint() → Value; ReadGuid().
        /// </summary>
        public static void Serialize(this ExtendedGUID32BitUintValue obj, BinaryWriter writer)
        {
            writer.Write(obj.Type); // 0x80
            writer.Write(obj.Value);
            writer.Write(obj.GUID.ToByteArray());
        }

        // ───────────────────────────────────────────────
        // 2.2.1.8  Extended GUID Array
        // ───────────────────────────────────────────────

        public static void Serialize(this ExtendedGUIDArray obj, BinaryWriter writer)
        {
            obj.Count.Serialize(writer);
            if (obj.Content != null)
            {
                for (int i = 0; i < obj.Content.Length; i++)
                {
                    obj.Content[i].Serialize(writer);
                }
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.1.9  Serial Number
        // ───────────────────────────────────────────────

        public static void Serialize(this SerialNumber obj, BinaryWriter writer)
        {
            if (obj is SerialNumberNullValue sn)
            {
                sn.Serialize(writer);
            }
            else if (obj is SerialNumber64BitUintValue sv)
            {
                sv.Serialize(writer);
            }
            else
            {
                throw new InvalidOperationException("Unknown SerialNumber subtype.");
            }
        }

        /// <summary>
        /// Parser: ReadByte() → Type = 0x00.
        /// </summary>
        public static void Serialize(this SerialNumberNullValue obj, BinaryWriter writer)
        {
            writer.Write(obj.Type); // 0x00
        }

        /// <summary>
        /// Parser: ReadByte() → Type=0x80; ReadGuid(); ReadUlong().
        /// </summary>
        public static void Serialize(this SerialNumber64BitUintValue obj, BinaryWriter writer)
        {
            writer.Write(obj.Type); // 0x80
            writer.Write(obj.GUID.ToByteArray());
            writer.Write(obj.Value);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.10  Cell ID
        // ───────────────────────────────────────────────

        public static void Serialize(this CellID obj, BinaryWriter writer)
        {
            obj.EXGUID1.Serialize(writer);
            obj.EXGUID2.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.11  Cell ID Array
        // ───────────────────────────────────────────────

        public static void Serialize(this CellIDArray obj, BinaryWriter writer)
        {
            obj.Count.Serialize(writer);
            if (obj.Content != null)
            {
                for (int i = 0; i < obj.Content.Length; i++)
                {
                    obj.Content[i].Serialize(writer);
                }
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12  Data Element Package
        // ───────────────────────────────────────────────

        public static void Serialize(this DataElementPackage obj, BinaryWriter writer)
        {
            obj.DataElementPackageStart.Serialize(writer);
            writer.Write(obj.Reserved);

            if (obj.DataElements != null)
            {
                foreach (var element in obj.DataElements)
                {
                    if (element is StorageIndexDataElement si)
                        si.Serialize(writer);
                    else if (element is StorageManifestDataElement sm)
                        sm.Serialize(writer);
                    else if (element is CellManifestDataElement cm)
                        cm.Serialize(writer);
                    else if (element is RevisionManifestDataElement rm)
                        rm.Serialize(writer);
                    else if (element is ObjectGroupDataElements og)
                        og.Serialize(writer);
                    else if (element is DataElementFragmentDataElement df)
                        df.Serialize(writer);
                    else if (element is ObjectDataBLOBDataElements ob)
                        ob.Serialize(writer);
                    else
                        throw new InvalidOperationException("Unknown DataElement type in DataElementPackage.");
                }
            }

            obj.DataElementPackageEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.2  Storage Index Data Element
        // ───────────────────────────────────────────────

        public static void Serialize(this StorageIndexDataElement obj, BinaryWriter writer)
        {
            obj.DataElementStart.Serialize(writer);
            obj.DataElementExtendedGUID.Serialize(writer);
            obj.SerialNumber.Serialize(writer);
            obj.DataElementType.Serialize(writer);

            if (obj.StorageIndexDataElementData != null)
            {
                foreach (var item in obj.StorageIndexDataElementData)
                {
                    if (item is StorageIndexManifestMappingValues mm)
                        mm.Serialize(writer);
                    else if (item is StorageIndexCellMappingValues cm)
                        cm.Serialize(writer);
                    else if (item is StorageIndexRevisionMappingValues rm)
                        rm.Serialize(writer);
                    else
                        throw new InvalidOperationException("Unknown StorageIndex mapping type.");
                }
            }

            obj.DataElementEnd.Serialize(writer);
        }

        public static void Serialize(this StorageIndexManifestMappingValues obj, BinaryWriter writer)
        {
            obj.StorageIndexManifestMapping.Serialize(writer);
            obj.ManifestMappingExtendedGUID.Serialize(writer);
            obj.ManifestMappingSerialNumber.Serialize(writer);
        }

        public static void Serialize(this StorageIndexCellMappingValues obj, BinaryWriter writer)
        {
            obj.StorageIndexCellMapping.Serialize(writer);
            obj.CellID.Serialize(writer);
            obj.CellMappingExtendedGUID.Serialize(writer);
            obj.CellMappingSerialNumber.Serialize(writer);
        }

        public static void Serialize(this StorageIndexRevisionMappingValues obj, BinaryWriter writer)
        {
            obj.StorageIndexRevisionMapping.Serialize(writer);
            obj.RevisionExtendedGUID.Serialize(writer);
            obj.RevisionMappingExtendedGUID.Serialize(writer);
            obj.RevisionMappingSerialNumber.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.3  Storage Manifest Data Element
        // ───────────────────────────────────────────────

        public static void Serialize(this StorageManifestDataElement obj, BinaryWriter writer)
        {
            obj.DataElementStart.Serialize(writer);
            obj.DataElementExtendedGUID.Serialize(writer);
            obj.SerialNumber.Serialize(writer);
            obj.DataElementType.Serialize(writer);
            obj.StorageManifestSchemaGUID.Serialize(writer);
            writer.Write(obj.GUID.ToByteArray());

            if (obj.StorageManifestRootDeclare != null)
            {
                foreach (var rd in obj.StorageManifestRootDeclare)
                {
                    rd.Serialize(writer);
                }
            }

            obj.DataElementEnd.Serialize(writer);
        }

        public static void Serialize(this StorageManifestRootDeclareValues obj, BinaryWriter writer)
        {
            obj.StorageManifestRootDeclare.Serialize(writer);
            obj.RootExtendedGUID.Serialize(writer);
            obj.CellID.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.4  Cell Manifest Data Element
        // ───────────────────────────────────────────────

        public static void Serialize(this CellManifestDataElement obj, BinaryWriter writer)
        {
            obj.DataElementStart.Serialize(writer);
            obj.DataElementExtendedGUID.Serialize(writer);
            obj.SerialNumber.Serialize(writer);
            obj.DataElementType.Serialize(writer);
            obj.CellManifestCurrentRevision.Serialize(writer);
            obj.CellManifestCurrentRevisionExtendedGUID.Serialize(writer);
            obj.DataElementEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.5  Revision Manifest Data Element
        // ───────────────────────────────────────────────

        public static void Serialize(this RevisionManifestDataElement obj, BinaryWriter writer)
        {
            obj.DataElementStart.Serialize(writer);
            obj.DataElementExtendedGUID.Serialize(writer);
            obj.SerialNumber.Serialize(writer);
            obj.DataElementType.Serialize(writer);
            obj.RevisionManifest.Serialize(writer);
            obj.RevisionID.Serialize(writer);
            obj.BaseRevisionID.Serialize(writer);

            if (obj.RevisionManifestDataElementsData != null)
            {
                foreach (var item in obj.RevisionManifestDataElementsData)
                {
                    if (item is RevisionManifestRootDeclareValues rd)
                        rd.Serialize(writer);
                    else if (item is RevisionManifestObjectGroupReferencesValues ogr)
                        ogr.Serialize(writer);
                    else
                        throw new InvalidOperationException("Unknown RevisionManifest sub-element type.");
                }
            }

            obj.DataElementEnd.Serialize(writer);
        }

        public static void Serialize(this RevisionManifestRootDeclareValues obj, BinaryWriter writer)
        {
            obj.RevisionManifestRootDeclare.Serialize(writer);
            obj.RootExtendedGUID.Serialize(writer);
            obj.ObjectExtendedGUID.Serialize(writer);
        }

        public static void Serialize(this RevisionManifestObjectGroupReferencesValues obj, BinaryWriter writer)
        {
            obj.RevisionManifestObjectGroupReferences.Serialize(writer);
            obj.ObjectGroupExtendedGUID.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.6  Object Group Data Elements
        // ───────────────────────────────────────────────

        public static void Serialize(this ObjectGroupDataElements obj, BinaryWriter writer)
        {
            obj.DataElementStart.Serialize(writer);
            obj.DataElementExtendedGUID.Serialize(writer);
            obj.SerialNumber.Serialize(writer);
            obj.DataElementType.Serialize(writer);

            if (obj.DataElementHash != null)
            {
                obj.DataElementHash.Serialize(writer);
            }

            obj.ObjectGroupDeclarationsStart.Serialize(writer);

            if (obj.ObjectDeclarationOrObjectDataBLOBDeclaration != null)
            {
                foreach (var decl in obj.ObjectDeclarationOrObjectDataBLOBDeclaration)
                {
                    if (decl is ObjectDeclaration od)
                        od.Serialize(writer);
                    else if (decl is ObjectDataBLOBDeclaration bd)
                        bd.Serialize(writer);
                    else
                        throw new InvalidOperationException("Unknown declaration type in ObjectGroup.");
                }
            }

            obj.ObjectGroupDeclarationsEnd.Serialize(writer);

            if (obj.ObjectMetadataDeclaration != null)
            {
                obj.ObjectMetadataDeclaration.Serialize(writer);
            }

            obj.ObjectGroupDataStart.Serialize(writer);

            if (obj.ObjectDataOrObjectDataBLOBReference != null)
            {
                foreach (var data in obj.ObjectDataOrObjectDataBLOBReference)
                {
                    if (data is ObjectData od)
                        od.Serialize(writer);
                    else if (data is ObjectDataBLOBReference br)
                        br.Serialize(writer);
                    else
                        throw new InvalidOperationException("Unknown data type in ObjectGroup.");
                }
            }

            obj.ObjectGroupDataEnd.Serialize(writer);
            obj.DataElementEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.6.1  Object Declaration
        // ───────────────────────────────────────────────

        public static void Serialize(this ObjectDeclaration obj, BinaryWriter writer)
        {
            obj.ObjectGroupObjectDeclaration.Serialize(writer);
            obj.ObjectExtendedGUID.Serialize(writer);
            obj.ObjectPartitionID.Serialize(writer);
            obj.ObjectDataSize.Serialize(writer);
            obj.ObjectReferencesCount.Serialize(writer);
            obj.CellReferencesCount.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.6.2  ObjectDataBLOBDeclaration
        // ───────────────────────────────────────────────

        public static void Serialize(this ObjectDataBLOBDeclaration obj, BinaryWriter writer)
        {
            obj.ObjectGroupObjectDataBLOBDeclaration.Serialize(writer);
            obj.ObjectExtendedGUID.Serialize(writer);
            obj.ObjectDataBLOBEXGUID.Serialize(writer);
            obj.ObjectPartitionID.Serialize(writer);
            obj.ObjectReferencesCount.Serialize(writer);
            obj.CellReferencesCount.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.6.3  Object Metadata Declaration
        // ───────────────────────────────────────────────

        public static void Serialize(this ObjectMetadataDeclaration obj, BinaryWriter writer)
        {
            obj.ObjectGroupMetadataDeclarations.Serialize(writer);
            if (obj.ObjectMetadata != null)
            {
                foreach (var md in obj.ObjectMetadata)
                {
                    md.Serialize(writer);
                }
            }
            obj.ObjectGroupMetadataDeclarationsEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.6.3.1  Object Metadata
        // ───────────────────────────────────────────────

        public static void Serialize(this ObjectMetadata obj, BinaryWriter writer)
        {
            obj.ObjectGroupMetadata.Serialize(writer);
            obj.ObjectChangeFrequency.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.6.4  Object Data
        // ───────────────────────────────────────────────

        public static void Serialize(this ObjectData obj, BinaryWriter writer)
        {
            obj.ObjectGroupObjectDataOrExcludedData.Serialize(writer);
            obj.ObjectExtendedGUIDArray.Serialize(writer);
            obj.CellIDArray.Serialize(writer);
            obj.DataSize.Serialize(writer);

            // The parser checks for IntermediateNode, LeafNode, or raw bytes.
            // Serializer writes whatever Data object is present.
            if (obj.Data != null)
            {
                if (obj.Data is byte[] rawBytes)
                {
                    writer.Write(rawBytes);
                }
                // If Data is some other parsed structure, callers must serialize it themselves
                // or store the raw bytes. For round-trip the parser stores byte[] for raw data.
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.6.5  Object Data BLOB Reference
        // ───────────────────────────────────────────────

        public static void Serialize(this ObjectDataBLOBReference obj, BinaryWriter writer)
        {
            obj.ObjectGroupObjectDataBLOBReference.Serialize(writer);
            obj.ObjectExtendedGUIDArray.Serialize(writer);
            obj.CellIDArray.Serialize(writer);
            obj.BLOBExtendedGUID.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.6.6  Data Element Hash
        // ───────────────────────────────────────────────

        public static void Serialize(this DataElementHash obj, BinaryWriter writer)
        {
            obj.DataElementHashDeclaration.Serialize(writer);
            obj.DataElementHashScheme.Serialize(writer);
            obj.DataElementHashData.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.7  Data Element Fragment Data Element
        // ───────────────────────────────────────────────

        public static void Serialize(this DataElementFragmentDataElement obj, BinaryWriter writer)
        {
            obj.DataElementStart.Serialize(writer);
            obj.DataElementExtendedGUID.Serialize(writer);
            obj.SerialNumber.Serialize(writer);
            obj.DataElementType.Serialize(writer);
            obj.DataElementFragment.Serialize(writer);
            obj.FragmentExtendedGUID.Serialize(writer);
            obj.FragmentDataElementSize.Serialize(writer);
            obj.FragmentFileChunkReference.Serialize(writer);
            obj.FragmentData.Serialize(writer);
            obj.DataElementEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.12.8  Object Data BLOB Data Elements
        // ───────────────────────────────────────────────

        public static void Serialize(this ObjectDataBLOBDataElements obj, BinaryWriter writer)
        {
            obj.DataElementStart.Serialize(writer);
            obj.DataElementExtendedGUID.Serialize(writer);
            obj.SerialNumber.Serialize(writer);
            obj.DataElementType.Serialize(writer);
            obj.ObjectDataBLOB.Serialize(writer);
            obj.Data.Serialize(writer);
            obj.DataElementEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13  Knowledge
        // ───────────────────────────────────────────────

        public static void Serialize(this Knowledge obj, BinaryWriter writer)
        {
            obj.KnowledgeStart.Serialize(writer);
            if (obj.SpecializedKnowledge != null)
            {
                foreach (var sk in obj.SpecializedKnowledge)
                {
                    sk.Serialize(writer);
                }
            }
            obj.KnowledgeEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.1  Specialized Knowledge
        // ───────────────────────────────────────────────

        public static void Serialize(this SpecializedKnowledge obj, BinaryWriter writer)
        {
            obj.SpecializedKnowledgeStart.Serialize(writer);
            writer.Write(obj.GUID.ToByteArray());

            if (obj.SpecializedKnowledgeData is CellKnowLedge ck)
                ck.Serialize(writer);
            else if (obj.SpecializedKnowledgeData is WaterlineKnowledge wk)
                wk.Serialize(writer);
            else if (obj.SpecializedKnowledgeData is FragmentKnowledge fk)
                fk.Serialize(writer);
            else if (obj.SpecializedKnowledgeData is ContentTagKnowledge ctk)
                ctk.Serialize(writer);
            else if (obj.SpecializedKnowledgeData is VersionTokenKnowledge vtk)
                vtk.Serialize(writer);
            else
                throw new InvalidOperationException("Unknown SpecializedKnowledge data type.");

            obj.SpecializedKnowledgeEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.2  Cell Knowledge
        // ───────────────────────────────────────────────

        public static void Serialize(this CellKnowLedge obj, BinaryWriter writer)
        {
            obj.CellKnowledgeStart.Serialize(writer);
            if (obj.CellKnowledgeData != null)
            {
                foreach (var item in obj.CellKnowledgeData)
                {
                    if (item is CellKnowledgeRange ckr)
                        ckr.Serialize(writer);
                    else if (item is CellKnowledgeEntry cke)
                        cke.Serialize(writer);
                    else
                        throw new InvalidOperationException("Unknown CellKnowledge data type.");
                }
            }
            obj.CellKnowledgeEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.2.1  Cell Knowledge Range
        // ───────────────────────────────────────────────

        public static void Serialize(this CellKnowledgeRange obj, BinaryWriter writer)
        {
            obj.cellKnowledgeRange.Serialize(writer);
            writer.Write(obj.GUID.ToByteArray());
            obj.From.Serialize(writer);
            obj.To.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.2.2  Cell Knowledge Entry
        // ───────────────────────────────────────────────

        public static void Serialize(this CellKnowledgeEntry obj, BinaryWriter writer)
        {
            obj.cellKnowledgeEntry.Serialize(writer);
            obj.SerialNumber.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.3  Fragment Knowledge
        // ───────────────────────────────────────────────

        public static void Serialize(this FragmentKnowledge obj, BinaryWriter writer)
        {
            obj.FragmentKnowledgeStart.Serialize(writer);
            if (obj.FragmentKnowledgeEntries != null)
            {
                foreach (var entry in obj.FragmentKnowledgeEntries)
                {
                    entry.Serialize(writer);
                }
            }
            obj.FragmentKnowledgeEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.3  Version Token Knowledge
        // ───────────────────────────────────────────────

        public static void Serialize(this VersionTokenKnowledge obj, BinaryWriter writer)
        {
            obj.VersionTokenKnowledgeStart.Serialize(writer);
            if (obj.TokenData != null)
            {
                writer.Write(obj.TokenData);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.3.1  Fragment Knowledge Entry
        // ───────────────────────────────────────────────

        public static void Serialize(this FragmentKnowledgeEntry obj, BinaryWriter writer)
        {
            obj.FragmentDescriptor.Serialize(writer);
            obj.ExtendedGUID.Serialize(writer);
            obj.DataElementSize.Serialize(writer);
            obj.DataElementChunkReference.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.4  Waterline Knowledge
        // ───────────────────────────────────────────────

        public static void Serialize(this WaterlineKnowledge obj, BinaryWriter writer)
        {
            obj.WaterlineKnowledgeStart.Serialize(writer);
            if (obj.WaterlineKnowledgeData != null)
            {
                foreach (var entry in obj.WaterlineKnowledgeData)
                {
                    entry.Serialize(writer);
                }
            }
            obj.WaterlineKnowledgeEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.4.1  Waterline Knowledge Entry
        // ───────────────────────────────────────────────

        public static void Serialize(this WaterlineKnowledgeEntry obj, BinaryWriter writer)
        {
            obj.waterlineKnowledgeEntry.Serialize(writer);
            obj.CellStorageExtendedGUID.Serialize(writer);
            obj.Waterline.Serialize(writer);
            obj.Reserved.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.5  Content Tag Knowledge
        // ───────────────────────────────────────────────

        public static void Serialize(this ContentTagKnowledge obj, BinaryWriter writer)
        {
            obj.ContentTagKnowledgeStart.Serialize(writer);
            if (obj.ContentTagKnowledgeEntryArray != null)
            {
                foreach (var entry in obj.ContentTagKnowledgeEntryArray)
                {
                    entry.Serialize(writer);
                }
            }
            obj.ContentTagKnowledgeEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.1.13.5.1  Content Tag Knowledge Entry
        // ───────────────────────────────────────────────

        public static void Serialize(this ContentTagKnowledgeEntry obj, BinaryWriter writer)
        {
            obj.ContentTagKnowledgeEntryStart.Serialize(writer);
            obj.BLOBExtendedGUID.Serialize(writer);
            obj.ClockData.Serialize(writer);
        }

        // ═══════════════════════════════════════════════
        // 2.2.3  Response Message Syntax
        // ═══════════════════════════════════════════════

        public static void Serialize(this FsshttpbResponse obj, BinaryWriter writer)
        {
            writer.Write(obj.ProtocolVersion);
            writer.Write(obj.MinimumVersion);
            writer.Write(obj.Signature);
            obj.ResponseStart.Serialize(writer);

            // Parser: ReadByte() → Status=bits[0], Reserved=bits[1..7]
            byte statusByte = (byte)((obj.Status & 0x01) | ((obj.Reserved & 0x7F) << 1));
            writer.Write(statusByte);

            if (obj.Status == 0x1)
            {
                obj.ResponseError.Serialize(writer);
            }
            else
            {
                if (obj.DataElementPackage != null)
                {
                    obj.DataElementPackage.Serialize(writer);
                }

                if (obj.SubResponses != null)
                {
                    foreach (var sr in obj.SubResponses)
                    {
                        sr.Serialize(writer);
                    }
                }
            }

            obj.ResponseEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.3.1  Sub-Responses
        // ───────────────────────────────────────────────

        public static void Serialize(this FsshttpbSubResponse obj, BinaryWriter writer)
        {
            obj.SubResponseStart.Serialize(writer);
            obj.RequestID.Serialize(writer);
            obj.RequestType.Serialize(writer);

            // Parser: ReadByte() → Status=bits[0], Reserved=bits[1..7]
            byte statusByte = (byte)((obj.Status & 0x01) | ((obj.Reserved & 0x7F) << 1));
            writer.Write(statusByte);

            if (obj.Status == 0x1)
            {
                obj.ResponseError.Serialize(writer);
            }
            else
            {
                if (obj.SubResponseData is QueryAccessResponse qar)
                    qar.Serialize(writer);
                else if (obj.SubResponseData is QueryChangesResponse qcr)
                    qcr.Serialize(writer);
                else if (obj.SubResponseData is PutChangesResponse pcr)
                    pcr.Serialize(writer);
                else if (obj.SubResponseData is AllocateExtendedGUIDRange aeg)
                    aeg.Serialize(writer);
            }

            obj.SubResponseEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.3.1.1  Query Access Response
        // ───────────────────────────────────────────────

        public static void Serialize(this QueryAccessResponse obj, BinaryWriter writer)
        {
            obj.ReadAccessResponseStart.Serialize(writer);
            obj.ReadAccessResponseError.Serialize(writer);
            obj.ReadAccessResponseEnd.Serialize(writer);
            obj.WriteAccessResponseStart.Serialize(writer);
            obj.WriteAccessResponseError.Serialize(writer);
            obj.WriteAccessResponseEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.3.1.2  Query Changes Response
        // ───────────────────────────────────────────────

        public static void Serialize(this QueryChangesResponse obj, BinaryWriter writer)
        {
            obj.queryChangesResponse.Serialize(writer);
            obj.StorageIndexExtendedGUID.Serialize(writer);

            // Parser: ReadByte() → P=bits[0], Reserved=bits[1..7]
            byte pByte = (byte)((obj.P & 0x01) | ((obj.Reserved & 0x7F) << 1));
            writer.Write(pByte);

            obj.Knowledge.Serialize(writer);

            if (obj.FileHash != null)
            {
                obj.FileHash.Serialize(writer);
                obj.Type.Serialize(writer);
                obj.DataHash.Serialize(writer);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.3.1.3  Put Changes Response
        // ───────────────────────────────────────────────

        public static void Serialize(this PutChangesResponse obj, BinaryWriter writer)
        {
            if (obj.putChangesResponse != null)
            {
                obj.putChangesResponse.Serialize(writer);
                obj.AppliedStorageIndexId.Serialize(writer);
                obj.DataElementsAdded.Serialize(writer);
            }

            obj.ResultantKnowledge.Serialize(writer);

            if (obj.DiagnosticRequestOptionOutput != null)
            {
                obj.DiagnosticRequestOptionOutput.Serialize(writer);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.3.1.3.1  Diagnostic Request Option Output
        // ───────────────────────────────────────────────

        public static void Serialize(this DiagnosticRequesOptionOutput obj, BinaryWriter writer)
        {
            obj.diagnosticRequestOptionOutputHeader.Serialize(writer);
            // Parser: ReadByte() → Forced=bits[0], Reserved=bits[1..7]
            byte temp = (byte)((obj.Forced & 0x01) | ((obj.Reserved & 0x7F) << 1));
            writer.Write(temp);
        }

        // ───────────────────────────────────────────────
        // 2.2.3.1.4  Allocate Extended GUID Range Response
        // ───────────────────────────────────────────────

        public static void Serialize(this AllocateExtendedGUIDRange obj, BinaryWriter writer)
        {
            obj.AllocateExtendedGUIDRangeResponse.Serialize(writer);
            writer.Write(obj.GUIDComponent.ToByteArray());
            obj.IntegerRangeMin.Serialize(writer);
            obj.IntegerRangeMax.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.3.2  Response Error
        // ───────────────────────────────────────────────

        public static void Serialize(this ResponseError obj, BinaryWriter writer)
        {
            obj.ErrorStart.Serialize(writer);
            writer.Write(obj.ErrorTypeGUID.ToByteArray());

            if (obj.ErrorData is CellError ce)
                ce.Serialize(writer);
            else if (obj.ErrorData is ProtocolError pe)
                pe.Serialize(writer);
            else if (obj.ErrorData is Win32Error w32)
                w32.Serialize(writer);
            else if (obj.ErrorData is HRESULTError hr)
                hr.Serialize(writer);
            // ErrorData can be null for unknown error GUIDs

            if (obj.ErrorStringSupplementalInfoStart != null)
            {
                obj.ErrorStringSupplementalInfoStart.Serialize(writer);
                obj.ErrorStringSupplementalInfo.Serialize(writer);
            }

            if (obj.ChainedError != null)
            {
                obj.ChainedError.Serialize(writer);
            }

            obj.ErrorEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.3.2.1  Cell Error
        // ───────────────────────────────────────────────

        public static void Serialize(this CellError obj, BinaryWriter writer)
        {
            obj.ErrorCell.Serialize(writer);
            writer.Write((uint)obj.ErrorCode);
        }

        // ───────────────────────────────────────────────
        // 2.2.3.2.2  Protocol Error
        // ───────────────────────────────────────────────

        public static void Serialize(this ProtocolError obj, BinaryWriter writer)
        {
            obj.ErrorProtocol.Serialize(writer);
            writer.Write((uint)obj.ErrorCode);
        }

        // ───────────────────────────────────────────────
        // 2.2.3.2.3  Win32 Error
        // ───────────────────────────────────────────────

        public static void Serialize(this Win32Error obj, BinaryWriter writer)
        {
            obj.ErrorWin32.Serialize(writer);
            writer.Write(obj.ErrorCode);
        }

        // ───────────────────────────────────────────────
        // 2.2.3.2.4  HRESULT Error
        // ───────────────────────────────────────────────

        public static void Serialize(this HRESULTError obj, BinaryWriter writer)
        {
            obj.ErrorHRESULT.Serialize(writer);
            writer.Write(obj.ErrorCode);
        }

        // ═══════════════════════════════════════════════
        // 2.2.2  Request Message Syntax  (for completeness)
        // ═══════════════════════════════════════════════

        public static void Serialize(this FsshttpbRequest obj, BinaryWriter writer)
        {
            writer.Write(obj.ProtocolVersion);
            writer.Write(obj.MinimumVersion);
            writer.Write(obj.Signature);
            obj.RequestStart.Serialize(writer);

            if (obj.UserAgentStart != null)
            {
                obj.UserAgentStart.Serialize(writer);

                if (obj.UserAgentGUID != null)
                {
                    obj.UserAgentGUID.Serialize(writer);
                }
                if (obj.GUID.HasValue)
                {
                    writer.Write(obj.GUID.Value.ToByteArray());
                }
                if (obj.UserAgentClientAndPlatform != null)
                {
                    obj.UserAgentClientAndPlatform.Serialize(writer);
                    obj.ClientCount.Serialize(writer);
                    writer.Write(obj.ClientByteArray);
                    obj.PlatformCount.Serialize(writer);
                    writer.Write(obj.PlatformByteArray);
                }

                obj.UserAgentVersion.Serialize(writer);
                writer.Write(obj.Version);
                obj.UserAgentEnd.Serialize(writer);
            }

            if (obj.RequestHashingOptionsDeclaration != null)
            {
                obj.RequestHashingOptionsDeclaration.Serialize(writer);
                obj.RequestHasingSchema.Serialize(writer);
                // Parser: ReadByte() → A=bit0, B=bit1, C=bit2, D=bit3, E=bits[4..7]
                byte tempByte = (byte)(
                    ((obj.A ?? 0) & 0x01)
                  | (((obj.B ?? 0) & 0x01) << 1)
                  | (((obj.C ?? 0) & 0x01) << 2)
                  | (((obj.D ?? 0) & 0x01) << 3)
                  | (((obj.E ?? 0) & 0x0F) << 4));
                writer.Write(tempByte);
            }

            if (obj.CellRoundtrioOptions != null)
            {
                obj.CellRoundtrioOptions.Serialize(writer);
                // Parser: ReadByte() → F=bit0, G=bit1, H=bits[2..7]
                byte tempByte = (byte)(
                    ((obj.F ?? 0) & 0x01)
                  | (((obj.G ?? 0) & 0x01) << 1)
                  | (((obj.H ?? 0) & 0x3F) << 2));
                writer.Write(tempByte);
            }

            if (obj.SubRequest != null)
            {
                foreach (var sr in obj.SubRequest)
                {
                    sr.Serialize(writer);
                }
            }

            if (obj.DataElementPackage != null)
            {
                obj.DataElementPackage.Serialize(writer);
            }

            obj.RequestEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1  Sub-Requests
        // ───────────────────────────────────────────────

        public static void Serialize(this FsshttpbSubRequest obj, BinaryWriter writer)
        {
            obj.SubRequestStart.Serialize(writer);
            obj.RequestID.Serialize(writer);
            obj.RequestType.Serialize(writer);
            obj.Priority.Serialize(writer);

            if (obj.TargetPartitionId != null)
            {
                obj.TargetPartitionId.Serialize(writer);
            }

            ulong reqType = obj.RequestType.GetUint(obj.RequestType);
            if (reqType == 0x02 && obj.SubRequestData is QueryChangesRequest qcr)
                qcr.Serialize(writer);
            else if (reqType == 0x05 && obj.SubRequestData is PutChangesRequest pcr)
                pcr.Serialize(writer);
            else if (reqType == 0x0B && obj.SubRequestData is AllocateExtendedGUIDRangeRequest aer)
                aer.Serialize(writer);
            // 0x01 QueryAccessRequest has no data

            obj.SubRequestEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.1  Target Partition Id
        // ───────────────────────────────────────────────

        public static void Serialize(this TargetPartitionId obj, BinaryWriter writer)
        {
            obj.TargetPartitionIdStart.Serialize(writer);
            writer.Write(obj.PartitionIdGUID.ToByteArray());
            if (obj.TargetPartitionIdEnd != null)
            {
                obj.TargetPartitionIdEnd.Serialize(writer);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.3  Query Changes Request
        // ───────────────────────────────────────────────

        public static void Serialize(this QueryChangesRequest obj, BinaryWriter writer)
        {
            obj.queryChangesRequest.Serialize(writer);

            // Parser: ReadByte() → A..H = bits[0..7]
            byte flagByte = (byte)(
                (obj.A & 0x01)
              | ((obj.B & 0x01) << 1)
              | ((obj.C & 0x01) << 2)
              | ((obj.D & 0x01) << 3)
              | ((obj.E & 0x01) << 4)
              | ((obj.F & 0x01) << 5)
              | ((obj.G & 0x01) << 6)
              | ((obj.H & 0x01) << 7));
            writer.Write(flagByte);

            if (obj.queryChangesRequest.Length == 2)
            {
                byte extra = (byte)(
                    ((obj.UserContentEquivalentVersionOk ?? 0) & 0x01)
                  | (((obj.ReservedMustBeZero ?? 0) & 0x7F) << 1));
                writer.Write(extra);
            }

            if (obj.queryChangesRequestArguments != null)
            {
                obj.queryChangesRequestArguments.Serialize(writer);
                byte temp2 = (byte)(
                    ((obj.F2 ?? 0) & 0x01)
                  | (((obj.G2 ?? 0) & 0x01) << 1)
                  | (((obj.H2 ?? 0) & 0x3F) << 2));
                writer.Write(temp2);
            }

            obj.CellID.Serialize(writer);

            if (obj.QueryChangesDataConstraints != null)
            {
                obj.QueryChangesDataConstraints.Serialize(writer);
            }
            if (obj.MaximumDataElements != null)
            {
                obj.MaximumDataElements.Serialize(writer);
            }

            if (obj.QueryChangesVersioning != null)
            {
                obj.QueryChangesVersioning.Serialize(writer);
                if (obj.MajorVersionNumber.HasValue && obj.MinorVersionNumber.HasValue)
                {
                    writer.Write(obj.MajorVersionNumber.Value);
                    writer.Write(obj.MinorVersionNumber.Value);
                }
                else if (obj.VersionToken != null)
                {
                    writer.Write(obj.VersionToken);
                }
            }

            if (obj.QueryChangesFilters != null)
            {
                foreach (var f in obj.QueryChangesFilters)
                {
                    f.Serialize(writer);
                }
            }

            if (obj.Knowledge != null)
            {
                obj.Knowledge.Serialize(writer);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.3.1  Filters
        // ───────────────────────────────────────────────

        public static void Serialize(this Filter obj, BinaryWriter writer)
        {
            obj.QueryChangesFilterStart.Serialize(writer);
            writer.Write((byte)obj.FilterType);
            writer.Write(obj.FilterOperation);

            if (obj.QueryChangesFilterData is DataElementIDsFilter deif)
                deif.Serialize(writer);
            else if (obj.QueryChangesFilterData is DataElementTypeFilter detf)
                detf.Serialize(writer);
            else if (obj.QueryChangesFilterData is CellIDFilter cif)
                cif.Serialize(writer);
            else if (obj.QueryChangesFilterData is CustomFilter cf)
                cf.Serialize(writer);
            else if (obj.QueryChangesFilterData is HierarchyFilter hf)
                hf.Serialize(writer);
            // AllFilter and StorageIndexReferencedDataElementsFilter have no data

            obj.QueryChangesFilterEnd.Serialize(writer);

            if (obj.QueryChangesFilterFlags != null)
            {
                obj.QueryChangesFilterFlags.Serialize(writer);
                byte tempByte = (byte)(
                    ((obj.F ?? 0) & 0x01)
                  | (((obj.Reserved ?? 0) & 0x7F) << 1));
                writer.Write(tempByte);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.3.1.2  Data Element Type Filter
        // ───────────────────────────────────────────────

        public static void Serialize(this DataElementTypeFilter obj, BinaryWriter writer)
        {
            obj.QueryChangesFilterDataElementType.Serialize(writer);
            obj.DataElementType.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.3.1.4  Cell ID Filter
        // ───────────────────────────────────────────────

        public static void Serialize(this CellIDFilter obj, BinaryWriter writer)
        {
            obj.QueryChangesFilterCellID.Serialize(writer);
            obj.CellID.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.3.1.5  Custom Filter
        // ───────────────────────────────────────────────

        public static void Serialize(this CustomFilter obj, BinaryWriter writer)
        {
            obj.QueryChangesFilterSchemaSpecific.Serialize(writer);
            writer.Write(obj.SchemaGUID.ToByteArray());
            if (obj.SchemaFilterData != null)
            {
                writer.Write(obj.SchemaFilterData);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.3.1.6  Data Element IDs Filter
        // ───────────────────────────────────────────────

        public static void Serialize(this DataElementIDsFilter obj, BinaryWriter writer)
        {
            obj.QueryChangesFilterDataElementIDs.Serialize(writer);
            obj.DataElementIDs.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.3.1.7  Hierarchy Filter
        // ───────────────────────────────────────────────

        public static void Serialize(this HierarchyFilter obj, BinaryWriter writer)
        {
            obj.QueryChangesFilterHierarchy.Serialize(writer);
            writer.Write((byte)obj.Depth);
            obj.Count.Serialize(writer);
            if (obj.RootIndexKeyByteArray != null)
            {
                writer.Write(obj.RootIndexKeyByteArray);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.4  Put Changes Request
        // ───────────────────────────────────────────────

        public static void Serialize(this PutChangesRequest obj, BinaryWriter writer)
        {
            obj.putChangesRequest.Serialize(writer);
            obj.StorageIndexExtendedGUID.Serialize(writer);
            obj.ExpectedStorageIndexExtendedGUID.Serialize(writer);

            // Parser: ReadByte() → A..H = bits[0..7]
            byte flagByte = (byte)(
                (obj.A & 0x01)
              | ((obj.B & 0x01) << 1)
              | ((obj.C & 0x01) << 2)
              | ((obj.D & 0x01) << 3)
              | ((obj.E & 0x01) << 4)
              | ((obj.F & 0x01) << 5)
              | ((obj.G & 0x01) << 6)
              | ((obj.H & 0x01) << 7));
            writer.Write(flagByte);

            obj.ContenVersionCoherencyCheck.Serialize(writer);
            obj.AuthorLogins.Serialize(writer);
            writer.Write(obj.Reserved1);

            if (obj.AdditionalFlags != null)
            {
                obj.AdditionalFlags.Serialize(writer);
            }
            if (obj.LockId != null)
            {
                obj.LockId.Serialize(writer);
            }
            if (obj.ClientKnowledge != null)
            {
                obj.ClientKnowledge.Serialize(writer);
            }
            if (obj.DiagnosticRequestOptionInput != null)
            {
                obj.DiagnosticRequestOptionInput.Serialize(writer);
            }
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.4.1  Additional Flags
        // ───────────────────────────────────────────────

        public static void Serialize(this AdditionalFlags obj, BinaryWriter writer)
        {
            obj.AdditionalFlagsHeader.Serialize(writer);

            // Parser: ReadINT16() → A..F=bits[0..5], Reserved=bits[6..15]
            ushort temp = (ushort)(
                (obj.A & 0x01)
              | ((obj.B & 0x01) << 1)
              | ((obj.C & 0x01) << 2)
              | ((obj.D & 0x01) << 3)
              | ((obj.E & 0x01) << 4)
              | ((obj.F & 0x01) << 5)
              | ((obj.Reserved & 0x03FF) << 6));
            writer.Write(temp);

            obj.Reserved2.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.4.2  Lock Id
        // ───────────────────────────────────────────────

        public static void Serialize(this LockId obj, BinaryWriter writer)
        {
            obj.LockIdHeader.Serialize(writer);
            writer.Write(obj.LockIdGuid.ToByteArray());
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.4.3  Diagnostic Request Option Input
        // ───────────────────────────────────────────────

        public static void Serialize(this DiagnosticRequestOptionInput obj, BinaryWriter writer)
        {
            obj.DiagnosticRequestOptionInputHeader.Serialize(writer);
            // Parser: ReadByte() → A=bit0, Reserved=bits[1..7]
            byte temp = (byte)((obj.A & 0x01) | ((obj.Reserved & 0x7F) << 1));
            writer.Write(temp);
        }

        // ───────────────────────────────────────────────
        // 2.2.2.1.5  Allocate Extended GUID Range Request
        // ───────────────────────────────────────────────

        public static void Serialize(this AllocateExtendedGUIDRangeRequest obj, BinaryWriter writer)
        {
            obj.allocateExtendedGUIDRangeRequest.Serialize(writer);
            obj.RequestIdCount.Serialize(writer);
            writer.Write(obj.Reserved);
        }

        // ═══════════════════════════════════════════════
        // MS-FSSHTTPD Structures (FSSHTTPD.cs)
        // ═══════════════════════════════════════════════

        // ───────────────────────────────────────────────
        // 2.2.2.1  Intermediate Node Object Data
        // ───────────────────────────────────────────────

        public static void Serialize(this IntermediateNodeObjectData obj, BinaryWriter writer)
        {
            obj.IntermediateNodeStart.Serialize(writer);
            obj.SignatureHeader.Serialize(writer);
            obj.SignatureData.Serialize(writer);
            obj.DataSizeHeader.Serialize(writer);
            writer.Write(obj.DataSize); // ReadUlong() → 8 bytes LE
            obj.IntermediateNodeEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // 2.2.3.1  Leaf Node Object Data
        // ───────────────────────────────────────────────

        public static void Serialize(this LeafNodeObjectData obj, BinaryWriter writer)
        {
            obj.LeafNodeStart.Serialize(writer);
            obj.SignatureHeader.Serialize(writer);
            obj.SignatureData.Serialize(writer);
            obj.DataSizeHeader.Serialize(writer);
            writer.Write(obj.DataSize); // ReadUlong() → 8 bytes LE

            if (obj.LeafNodeDataHashHeader != null)
            {
                obj.LeafNodeDataHashHeader.Serialize(writer);
                obj.LeafNodeDataHash.Serialize(writer);
            }

            obj.LeafNodeEnd.Serialize(writer);
        }

        // ───────────────────────────────────────────────
        // ZIP File Structure
        // ───────────────────────────────────────────────

        public static void Serialize(this ZIPFileStructure obj, BinaryWriter writer)
        {
            if (obj.fileHeader != null)
            {
                foreach (var h in obj.fileHeader)
                {
                    h.Serialize(writer);
                }
            }

            if (obj.archiveExtraDataRecord != null)
            {
                obj.archiveExtraDataRecord.Serialize(writer);
            }

            if (obj.centralDirectory != null)
            {
                obj.centralDirectory.Serialize(writer);
            }

            if (obj.zip64EndOfCentralDirectoryRecord != null)
            {
                obj.zip64EndOfCentralDirectoryRecord.Serialize(writer);
            }

            if (obj.zip64EndOfCentralDirectoryLocator != null)
            {
                obj.zip64EndOfCentralDirectoryLocator.Serialize(writer);
            }

            if (obj.endOfCentralDirectoryRecord != null)
            {
                obj.endOfCentralDirectoryRecord.Serialize(writer);
            }
        }

        // ───────────────────────────────────────────────
        // Local File Header
        // ───────────────────────────────────────────────

        public static void Serialize(this LocalFileHeader obj, BinaryWriter writer)
        {
            writer.Write(obj.LocalFileHeaderSignature);
            writer.Write(obj.VersionNeededToExtract);
            writer.Write(obj.GeneralPurposeBitFlag);
            writer.Write(obj.CompressionMethod);
            writer.Write(obj.LastModFileTime);
            writer.Write(obj.LastModFileDate);
            writer.Write(obj.Crc32);
            writer.Write(obj.CompressedSize);
            writer.Write(obj.UncompressedSize);
            writer.Write(obj.FileNameLength);
            writer.Write(obj.ExtraFieldLength);

            if (obj.FileNameLength > 0 && obj.FileName != null)
            {
                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(obj.FileName);
                writer.Write(nameBytes);
            }

            if (obj.ExtraFieldLength > 0 && obj.ExtraField != null)
            {
                writer.Write(obj.ExtraField);
            }

            if (obj.CompressedSize > 0 && obj.FileData != null)
            {
                writer.Write(obj.FileData);
            }

            // Data descriptor: written when bit 3 of GeneralPurposeBitFlag is set
            if ((obj.GeneralPurposeBitFlag & 0x08) != 0)
            {
                if (obj.Signature != null)
                {
                    writer.Write(obj.Signature);
                }
                if (obj.Crc32_descriptor.HasValue)
                {
                    writer.Write(obj.Crc32_descriptor.Value);
                }
                if (obj.CompressedSize_descriptor.HasValue)
                {
                    writer.Write(obj.CompressedSize_descriptor.Value);
                }
                if (obj.UncompressedSize_descriptor.HasValue)
                {
                    writer.Write(obj.UncompressedSize_descriptor.Value);
                }
            }
        }

        // ───────────────────────────────────────────────
        // Archive Extra Data Record
        // ───────────────────────────────────────────────

        public static void Serialize(this ArchiveExtraDataRecord obj, BinaryWriter writer)
        {
            writer.Write(obj.ArchiveExtraDataSignature);
            writer.Write(obj.ExtraFieldLength);
            if (obj.ExtraFieldLength > 0 && obj.ExtraFieldData != null)
            {
                writer.Write(obj.ExtraFieldData);
            }
        }

        // ───────────────────────────────────────────────
        // Central Directory Structure
        // ───────────────────────────────────────────────

        public static void Serialize(this CentralDirectoryStructure obj, BinaryWriter writer)
        {
            if (obj.fileHeader != null)
            {
                foreach (var h in obj.fileHeader)
                {
                    h.Serialize(writer);
                }
            }

            if (obj.digitalSignature != null)
            {
                obj.digitalSignature.Serialize(writer);
            }
        }

        // ───────────────────────────────────────────────
        // Central Directory File Header
        // ───────────────────────────────────────────────

        public static void Serialize(this CentralDirectoryFileHeader obj, BinaryWriter writer)
        {
            writer.Write(obj.CentralFileHeaderSignature);
            writer.Write(obj.VersionMadeBy);
            writer.Write(obj.VersionNeededToExtract);
            writer.Write(obj.GeneralPurposeBitFlag);
            writer.Write(obj.CompressionMethod);
            writer.Write(obj.LastModFileTime);
            writer.Write(obj.LastModFileDate);
            writer.Write(obj.Crc32);
            writer.Write(obj.CompressedSize);
            writer.Write(obj.UncompressedSize);
            writer.Write(obj.FileNameLength);
            writer.Write(obj.ExtraFieldLength);
            writer.Write(obj.FileCommentLength);
            writer.Write(obj.DiskNumberStart);
            writer.Write(obj.InternalFileAttributes);
            writer.Write(obj.ExternalFileAttributes);
            writer.Write(obj.RelativeOffsetOfLocalHeader);

            if (obj.FileNameLength > 0 && obj.FileName != null)
            {
                writer.Write(obj.FileName);
            }

            if (obj.ExtraFieldLength > 0 && obj.ExtraField != null)
            {
                writer.Write(obj.ExtraField);
            }

            if (obj.FileCommentLength > 0 && obj.FileComment != null)
            {
                writer.Write(obj.FileComment);
            }
        }

        // ───────────────────────────────────────────────
        // Central Directory Digital Signature
        // ───────────────────────────────────────────────

        public static void Serialize(this CentralDirectoryDigitalSignature obj, BinaryWriter writer)
        {
            writer.Write(obj.HeaderSignature);
            writer.Write(obj.SizeOfData);
            if (obj.SizeOfData > 0 && obj.SignatureData != null)
            {
                writer.Write(obj.SignatureData);
            }
        }

        // ───────────────────────────────────────────────
        // Zip64 End of Central Directory Record
        // ───────────────────────────────────────────────

        public static void Serialize(this Zip64EndOfCentralDirectoryRecord obj, BinaryWriter writer)
        {
            writer.Write(obj.Zip64EndOfCentralDirSignature);
            writer.Write(obj.SizeOfZip64EndOfCentralDirectoryRecord);
            writer.Write(obj.VersionMadeBy);
            writer.Write(obj.VersionNeededToExtract);
            writer.Write(obj.NumberOfThisDisk);
            writer.Write(obj.NumberOfTheDiskWithTheStartOfTheCentralDirectory);
            writer.Write(obj.TotalNumberOfEntriesInTheCentralDirectoryOnThisDisk);
            writer.Write(obj.TotalNumberOfEntriesInTheCentralDirectory);
            writer.Write(obj.SizeOfTheCentralDirectory);
            writer.Write(obj.OffsetOfStartOfCentralDirectoryWithRespectToTheStartingDiskNumber);

            if (obj.Zip64ExtensibleDataSector != null)
            {
                writer.Write(obj.Zip64ExtensibleDataSector);
            }
        }

        // ───────────────────────────────────────────────
        // Zip64 End of Central Directory Locator
        // ───────────────────────────────────────────────

        public static void Serialize(this Zip64endOfCentralDirectoryLocator obj, BinaryWriter writer)
        {
            writer.Write(obj.Zip64EndOfCentralDirLocatorSignature);
            writer.Write(obj.NumberOfTheDiskWithTheStartOfTheZip64EndOfCentralDirectory);
            writer.Write(obj.RelativeOffsetOfTheZip64EndOfCentralDirectoryRecord);
            writer.Write(obj.TotalNumberOfDisks);
        }

        // ───────────────────────────────────────────────
        // End of Central Directory Record
        // ───────────────────────────────────────────────

        public static void Serialize(this EndOfCentralDirectoryRecord obj, BinaryWriter writer)
        {
            writer.Write(obj.EndOfCentralDirSignature);
            writer.Write(obj.NumberOfThisDisk);
            writer.Write(obj.NumberOfThisDiskWithTheStartOfTheCentralDirectory);
            writer.Write(obj.TotalNumberOfEntriesInTheCentralDirectoryOnThisDisk);
            writer.Write(obj.TotalNumberOfEntriesInTheCentralDirectory);
            writer.Write(obj.SizeOfTheCentralDirectory);
            writer.Write(obj.OffsetOfStartOfCentralDirectoryWithRespectToTheStartingDiskNumber);
            writer.Write(obj.ZipFileCommentLength);

            if (obj.ZipFileCommentLength > 0 && obj.ZipFileComment != null)
            {
                writer.Write(obj.ZipFileComment);
            }
        }
    }
}
