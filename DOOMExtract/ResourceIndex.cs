using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DOOMExtract
{
    public class DOOMResourceEntry
    {
        private DOOMResourceIndex _index;

        public int ID;
        public string FileType; // type?
        public string FileName2; // ??
        public string FileName3; // full path

        public long Offset;
        public int Size;
        public int CompressedSize;
        public long Zero;
        public byte Zero1;

        public DOOMResourceEntry(DOOMResourceIndex index)
        {
            _index = index;
        }

        public override string ToString()
        {
            return GetFullName();
        }

        public string GetFullName()
        {
            if (!String.IsNullOrEmpty(FileName3))
                return FileName3.Replace("/", "\\"); // convert to windows path

            if (!String.IsNullOrEmpty(FileName2))
                return FileName2.Replace("/", "\\"); // convert to windows path

            return FileType.Replace("/", "\\"); // convert to windows path

        }
        public void Read(EndianIO io)
        {
            io.BigEndian = true;
            ID = io.Reader.ReadInt32();

            io.BigEndian = false;
            // fname1
            int size = io.Reader.ReadInt32();
            FileType = io.Reader.ReadAsciiString(size);
            // fname2
            size = io.Reader.ReadInt32();
            FileName2 = io.Reader.ReadAsciiString(size);
            // fname3
            size = io.Reader.ReadInt32();
            FileName3 = io.Reader.ReadAsciiString(size);

            io.BigEndian = true;

            Offset = io.Reader.ReadInt64();
            Size = io.Reader.ReadInt32();
            CompressedSize = io.Reader.ReadInt32();
            if (_index.Header_Version <= 4)
                Zero = io.Reader.ReadInt64();
            else
                Zero = io.Reader.ReadInt32(); // Zero field is 4 bytes instead of 8 in version 5+

            Zero1 = io.Reader.ReadByte();
        }

        public void Write(EndianIO io)
        {
            io.BigEndian = true;
            io.Writer.Write(ID);

            io.BigEndian = false;
            io.Writer.Write(FileType.Length);
            io.Writer.WriteAsciiString(FileType, FileType.Length);
            io.Writer.Write(FileName2.Length);
            io.Writer.WriteAsciiString(FileName2, FileName2.Length);
            io.Writer.Write(FileName3.Length);
            io.Writer.WriteAsciiString(FileName3, FileName3.Length);

            io.BigEndian = true;

            io.Writer.Write(Offset);
            io.Writer.Write(Size);
            io.Writer.Write(CompressedSize);
            if (_index.Header_Version <= 4)
                io.Writer.Write(Zero);
            else
                io.Writer.Write((int)Zero); // Zero field is 4 bytes instead of 8 in version 5+
            io.Writer.Write(Zero1);
        }
    }
    public class DOOMResourceIndex
    {
        EndianIO indexIO;
        EndianIO resourceIO;

        public string IndexFilePath;
        public string ResourceFilePath;

        public byte Header_Version;
        public int Header_IndexSize;
        public int Header_NumEntries;

        public List<DOOMResourceEntry> Entries;

        public DOOMResourceIndex(string indexFilePath)
        {
            IndexFilePath = indexFilePath;
        }

        public byte[] GetEntryData(DOOMResourceEntry entry, bool decompress = true)
        {
            if (entry.Size == 0 && entry.CompressedSize == 0)
                return new byte[] { };

            resourceIO.Stream.Position = entry.Offset;
            var compressedData = resourceIO.Reader.ReadBytes(entry.CompressedSize);
            if (entry.Size == entry.CompressedSize || !decompress)
                return compressedData;

            // have to decompress
            byte[] data = DecompressData(compressedData);
            if (data.Length == entry.Size)
                return data;
            throw new Exception("Decompression failed");
        }

        public static byte[] DecompressData(byte[] data)
        {
            /* we have to make an array that's length+2 and set some byte to 3 for some reason? */
            byte[] dec = new byte[data.Length + 2];
            dec[dec.Length - 2] = 3;
            data.CopyTo(dec, 0);

            return new MemoryStream(ZLibNet.DeflateCompressor.DeCompress(dec)).ToArray();
        }

        public static byte[] CompressData(byte[] data, ZLibNet.CompressionLevel level = ZLibNet.CompressionLevel.Level9)
        {
            using (var dest = new MemoryStream())
            {
                using (var source = new MemoryStream(data))
                {
                    using (var deflateStream = new ZLibNet.DeflateStream(dest, ZLibNet.CompressionMode.Compress, level, true))
                    {
                        source.CopyTo(deflateStream);

                        /* DOOM's compressed resources all end with 00 00 FF FF */
                        dest.SetLength(dest.Length + 4);
                        dest.Position = dest.Length - 2;
                        dest.WriteByte(0xFF);
                        dest.WriteByte(0xFF);

                        /* DOOM's compressed resources all seem to have the first bit unset
                         * tested by decompressing data and then recompressing using this Compress method, data is 1:1 except for the first bit (eg. our compressed data started with 0x7D, the games data would be 0x7C)
                         * in one test, keeping the bit set made the games graphics screw up and trying to open multiplayer would crash
                         * but unsetting this bit let the game work normally (using a slightly modified decl file also)
                         * another test like this using data that was heavily modded still resulted in a glitched game, even with this bit unset
                         * results inconclusive :( */
                        /*dest.Position = 0;
                        byte b = (byte)ms.ReadByte();
                        b &= byte.MaxValue ^ (1 << 0);
                        ms.Position = 0;
                        ms.WriteByte((byte)b);*/

                        return dest.ToArray();
                    }
                }
            }
        }

        private void addFilesFromFolder(string folder, string baseFolder, EndianIO destResources, ref List<string> addedFiles)
        {
            var dirs = Directory.GetDirectories(folder);
            var files = Directory.GetFiles(folder);
            foreach(var file in files)
            {
                if (addedFiles.Contains(Path.GetFullPath(file)))
                    continue;

                var filePath = Path.GetFullPath(file).Substring(Path.GetFullPath(baseFolder).Length).Replace("\\", "/");
                var fileEntry = new DOOMResourceEntry(this);

                fileEntry.FileType = "file";
                if(filePath.Contains(";")) // fileType is specified
                {
                    var idx = filePath.IndexOf(";");
                    fileEntry.FileType = filePath.Substring(idx + 1);
                    filePath = filePath.Substring(0, idx);
                }
                fileEntry.FileName2 = filePath;
                fileEntry.FileName3 = filePath;

                if (destResources.Stream.Length % 0x10 != 0)
                {
                    int extra = 0x10 - ((int)destResources.Stream.Length % 0x10);
                    destResources.Stream.SetLength(destResources.Stream.Length + extra);
                }
                byte[] fileData = File.ReadAllBytes(file);
                fileEntry.Size = fileEntry.CompressedSize = fileData.Length;

                fileEntry.Offset = destResources.Stream.Length;
                fileEntry.ID = Entries.Count; // TODO: find out wtf the ID is needed for?
                destResources.Stream.Position = fileEntry.Offset;
                destResources.Writer.Write(fileData);

                Entries.Add(fileEntry);
                addedFiles.Add(Path.GetFullPath(file));
            }

            foreach(var dir in dirs)
            {
                addFilesFromFolder(dir, baseFolder, destResources, ref addedFiles);
            }
        }

        public void Rebuild(string destResourceFile, string replaceFromFolder = "", bool keepCompressed = false)
        {
            if (File.Exists(destResourceFile))
                File.Delete(destResourceFile);

            if (!String.IsNullOrEmpty(replaceFromFolder))
            {
                replaceFromFolder = replaceFromFolder.Replace("/", "\\");
                if (!replaceFromFolder.EndsWith("\\"))
                    replaceFromFolder += "\\";
            }

            var destResources = new EndianIO(destResourceFile, FileMode.CreateNew);
            byte[] header = { Header_Version, 0x53, 0x45, 0x52, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            destResources.Writer.Write(header);

            var addedFiles = new List<string>();
            foreach (var file in Entries)
            {
                if (destResources.Stream.Length % 0x10 != 0)
                {
                    int extra = 0x10 - ((int)destResources.Stream.Length % 0x10);
                    destResources.Stream.SetLength(destResources.Stream.Length + extra);
                }

                if (file.Size <= 0 && file.CompressedSize <= 0)
                {
                    file.Offset = (int)destResources.Stream.Length;
                    continue;
                }
                byte[] data = null;
                bool isCompressed = keepCompressed;

                var replacePath = (String.IsNullOrEmpty(replaceFromFolder) ? String.Empty : Path.Combine(replaceFromFolder, file.GetFullName()));
                if (File.Exists(replacePath + ";" + file.FileType))
                    replacePath += ";" + file.FileType;

                if(!String.IsNullOrEmpty(replaceFromFolder) && File.Exists(replacePath))
                {
                    data = File.ReadAllBytes(replacePath);
                    isCompressed = false;
                    addedFiles.Add(replacePath);
                }
                else
                    data = GetEntryData(file, !keepCompressed);

                file.Offset = destResources.Stream.Length;
                if(!isCompressed)
                    file.Size = data.Length;

                file.CompressedSize = data.Length;

                destResources.Stream.Position = file.Offset;
                destResources.Writer.Write(data);
            }

            // now add any files that weren't replaced
            if(!String.IsNullOrEmpty(replaceFromFolder))
                addFilesFromFolder(replaceFromFolder, replaceFromFolder, destResources, ref addedFiles);

            destResources.Close();
            Save();
        }

        public void Close()
        {
            if(indexIO != null)
            {
                indexIO.Close();
                indexIO = null;
            }
            if(resourceIO != null)
            {
                resourceIO.Close();
                resourceIO = null;
            }
        }

        public void Save()
        {
            indexIO.Stream.SetLength(0x20);

            indexIO.BigEndian = true;
            indexIO.Stream.Position = 0x20;
            indexIO.Writer.Write(Entries.Count);

            foreach (var file in Entries)
                file.Write(indexIO);

            indexIO.Stream.Position = 0;
            indexIO.BigEndian = false;
            byte[] header = { Header_Version, 0x53, 0x45, 0x52 };
            indexIO.Writer.Write(header);
            indexIO.BigEndian = true;
            indexIO.Writer.Write((int)indexIO.Stream.Length - 0x20); // size of index file minus header size
            indexIO.Stream.Flush();
        }

        public bool Load()
        {
            if (!File.Exists(IndexFilePath) || Path.GetExtension(IndexFilePath) != ".index")
                return false; // not an index file

            ResourceFilePath = Path.Combine(Path.GetDirectoryName(IndexFilePath), Path.GetFileNameWithoutExtension(IndexFilePath)) + ".resources";
            if (!File.Exists(ResourceFilePath))
                return false;

            indexIO = new EndianIO(IndexFilePath, FileMode.Open);
            resourceIO = new EndianIO(ResourceFilePath, FileMode.Open);

            indexIO.Stream.Position = 0;
            var magic = indexIO.Reader.ReadInt32();
            if ((magic & 0xFFFFFF00) != 0x52455300)
            {
                indexIO.Close();
                resourceIO.Close();
                return false; // not a RES file.
            }
            Header_Version = (byte)(magic & 0xFF);
            Header_IndexSize = indexIO.Reader.ReadInt32();

            resourceIO.Stream.Position = 0;
            magic = resourceIO.Reader.ReadInt32();
            if((magic & 0xFFFFFF00) != 0x52455300)
            {
                indexIO.Close();
                resourceIO.Close();
                return false;
            }

            indexIO.Stream.Position = 0x20;
            indexIO.BigEndian = true;
            Header_NumEntries = indexIO.Reader.ReadInt32();

            Entries = new List<DOOMResourceEntry>();
            for (var i = 0; i < Header_NumEntries; i++)
            {
                var entry = new DOOMResourceEntry(this);
                entry.Read(indexIO);
                Entries.Add(entry);
            }

            return true;
        }
    }
}
