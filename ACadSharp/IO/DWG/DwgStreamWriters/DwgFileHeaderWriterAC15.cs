using CSUtilities.Converters;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ACadSharp.IO.DWG
{
    /*
    HEADER
        FILE HEADER
        DWG HEADER VARIABLES
        CRC
    CLASS DEFINITIONS
    TEMPLATE (R13 only, optional)
    PADDING (R13C3 AND LATER, 200 bytes, minutes the template section above if present)
    IMAGE DATA (PRE-R13C3)
    OBJECT DATA
        All entities, table entries, dictionary entries, etc. go in this section.
    OBJECT MAP
    OBJECT FREE SPACE (optional)
    TEMPLATE (R14-R15, optional)
    SECOND HEADER
    IMAGE DATA (R13C3 AND LATER)
    */
    internal class DwgFileHeaderWriterAC15 : DwgFileHeaderWriterBase
    {
        private readonly Dictionary<string, RecordEntry> _records;

        private byte[] _endSentinel = new byte[16]
        {
            0x95,0xA0,0x4E,0x28,0x99,0x82,0x1A,0xE5,0x5E,0x41,0xE0,0x5F,0x9D,0x3A,0x4D,0x00
        };

        protected override int _fileHeaderSize { get { return 0x61; } }

        public override int HandleSectionOffset
        {
            get
            {
                long offset = _fileHeaderSize;

                foreach (var item in this._records)
                {
                    if (item.Key == DwgSectionDefinition.AcDbObjects)
                        break;

                    if (item.Value.Stream != null)
                        offset += item.Value.Stream.Length;
                }

                return (int)offset;
            }
        }

        public DwgFileHeaderWriterAC15(Stream stream, Encoding encoding, CadDocument model)
            : base(stream, encoding, model)
        {
            _records = new Dictionary<string, RecordEntry>
            {
                { DwgSectionDefinition.Header      , new RecordEntry(new DwgSectionLocatorRecord(0), null) },
                { DwgSectionDefinition.Classes     , new RecordEntry(new DwgSectionLocatorRecord(1), null) },
                { DwgSectionDefinition.ObjFreeSpace, new RecordEntry(new DwgSectionLocatorRecord(3), null) },
                { DwgSectionDefinition.Template    , new RecordEntry(new DwgSectionLocatorRecord(4), null) },
                { DwgSectionDefinition.AuxHeader   , new RecordEntry(new DwgSectionLocatorRecord(5), null) },
                { DwgSectionDefinition.AcDbObjects , new RecordEntry(new DwgSectionLocatorRecord(null), null) },
                { DwgSectionDefinition.Handles     , new RecordEntry(new DwgSectionLocatorRecord(2), null) },
                { DwgSectionDefinition.Preview     , new RecordEntry(new DwgSectionLocatorRecord(null), null) },
            };
        }

        public override void AddSection(string name, MemoryStream stream, bool isCompressed, int decompsize = 0x7400)
        {
            var entry = this._records[name];
            entry.Record.Size = stream.Length;
            entry.Stream = stream;
            this._records[name] = entry;
        }

        public override void WriteFile()
        {
            setRecordSeekers();
            writeFileHeader();
            writeRecordStreams();
        }

        private void setRecordSeekers()
        {
            long currOffset = _fileHeaderSize;
            foreach (var item in this._records.Values)
            {
                item.Record.Seeker = currOffset;
                if (item.Stream != null)
                    currOffset += item.Stream.Length;
            }
        }

        private void writeFileHeader()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                IDwgStreamWriter writer = DwgStreamWriterBase.GetStreamWriter(this._version, memoryStream, this._encoding);

                // Version string
                writer.WriteBytes(Encoding.ASCII.GetBytes(this._document.Header.VersionString));

                // Next 7 bytes (5 zeros + ACADMAINTVER + 1)
                writer.WriteBytes(new byte[7] { 0, 0, 0, 0, 0, 15, 1 });

                // Seeker for preview section
                writer.WriteRawLong(this._records[DwgSectionDefinition.Preview].Record.Seeker);

                writer.WriteByte(0x1B);
                writer.WriteByte(0x19);

                // Code page
                writer.WriteBytes(LittleEndianConverter.Instance.GetBytes(this.getFileCodePage()));
                writer.WriteBytes(LittleEndianConverter.Instance.GetBytes(6));

                // Write section records
                foreach (var entry in this._records.Values)
                {
                    if (entry.Record.Number.HasValue)
                        writeRecord(writer, entry.Record);
                }

                // CRC
                writer.WriteSpearShift();
                writer.WriteRawShort((short)CRC8StreamHandler.GetCRCValue(0xC0C1, memoryStream.GetBuffer(), 0L, memoryStream.Length));

                // End sentinel
                writer.WriteBytes(_endSentinel);

                this._stream.Write(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            }
        }

        private void writeRecord(IDwgStreamWriter writer, DwgSectionLocatorRecord record)
        {
            writer.WriteByte((byte)record.Number.Value);
            writer.WriteRawLong(record.Seeker);
            writer.WriteRawLong(record.Size);
        }

        private void writeRecordStreams()
        {
            foreach (var item in this._records.Values)
            {
                if (item.Stream != null)
                    this._stream.Write(item.Stream.GetBuffer(), 0, (int)item.Stream.Length);
            }
        }

        /// <summary>
        /// Helper class to replace tuple in _records dictionary
        /// </summary>
        private class RecordEntry
        {
            public DwgSectionLocatorRecord Record { get; set; }
            public MemoryStream Stream { get; set; }

            public RecordEntry(DwgSectionLocatorRecord record, MemoryStream stream)
            {
                Record = record;
                Stream = stream;
            }
        }
    }
}