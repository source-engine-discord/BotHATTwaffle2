using System;
using System.IO;
using static BotHATTwaffle2.Models.BSP.SourceBSPStructs;

namespace BotHATTwaffle2.Services.Steam
{
    public class SourceBSP
    {
        public dheader_t Header;
        public byte[] PAKFILE;

        public SourceBSP(string filepath)
        {
            using (var FS = File.OpenRead(filepath))
            using (var BR = new BinaryReader(FS))
            {
                #region Header

                Header = new dheader_t();
                Header.ident = BR.ReadInt32();
                if (Header.ident != Constants.IDBSPHEADER)
                    throw new Exception($"Its not BSP!\n Path: {filepath}");

                Header.version = BR.ReadInt32();
                if (Header.version != 21)
                    throw new Exception($"Not CS:GO map!\n Map version:{Header.version} \n Path: {filepath}");

                Header.lumps = new lump_t[Constants.HEADER_LUMPS];
                for (var i = 0; i < Constants.HEADER_LUMPS; i++)
                    Header.lumps[i] = ReadLump(BR);
                Header.mapRevision = BR.ReadInt32();

                #endregion

                #region PAKFILE #40

                var pakfile_lump = Header.lumps[(int) Lumps.LUMP_PAKFILE];
                BR.BaseStream.Seek(pakfile_lump.fileofs, SeekOrigin.Begin);
                PAKFILE = BR.ReadBytes(pakfile_lump.filelen);

                #endregion
            }
        }

        public lump_t ReadLump(BinaryReader BR)
        {
            return new lump_t
            {
                fileofs = BR.ReadInt32(),
                filelen = BR.ReadInt32(),
                version = BR.ReadInt32(),
                fourCC = new char[4] {BR.ReadChar(), BR.ReadChar(), BR.ReadChar(), BR.ReadChar()}
            };
        }
    }
}