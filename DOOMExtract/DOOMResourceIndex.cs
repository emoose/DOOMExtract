using System;
using System.Collections.Generic;
using System.IO;

namespace DOOMExtract
{
    public class DOOMResourceIndex
    {
        EndianIO indexIO;
        Dictionary<string, EndianIO> resourceIOs;

        public byte PatchFileNumber = 0; // highest PatchFileNumber found in the entries of this index

        public string IndexFilePath;
        public string BaseIndexFilePath
        {
            get
            {
                var sepString = "resources_";
                var indexPath = IndexFilePath;
                var numSepIdx = indexPath.IndexOf(sepString);
                if (numSepIdx < 0)
                    return indexPath; // IndexFilePath is the base index?

                var basePath = indexPath.Substring(0, numSepIdx + sepString.Length - 1);
                return basePath + ".index";
            }
        }

        public byte Header_Version;
        public int Header_IndexSize;
        public int Header_NumEntries;

        public List<DOOMResourceEntry> Entries;

        public DOOMResourceIndex(string indexFilePath)
        {
            IndexFilePath = indexFilePath;
        }

        public string ResourceFilePath(int patchFileNumber)
        {
            var path = Path.Combine(Path.GetDirectoryName(BaseIndexFilePath), Path.GetFileNameWithoutExtension(BaseIndexFilePath));
            if (patchFileNumber == 0)
                return path + ".resources";
            if (patchFileNumber == 1)
                return path + ".patch";

            return $"{path}_{patchFileNumber:D3}.patch";
        }

        public EndianIO GetResourceIO(int patchFileNumber)
        {
            var resPath = ResourceFilePath(patchFileNumber);
            if (!resourceIOs.ContainsKey(resPath))
            {
                if (!File.Exists(resPath))
                    return null;

                var io = new EndianIO(resPath, FileMode.Open);
                io.Stream.Position = 0;
                var magic = io.Reader.ReadUInt32();
                if ((magic & 0xFFFFFF00) != 0x52455300)
                {
                    io.Close();
                    return null;
                }

                resourceIOs.Add(resPath, io);
            }

            return resourceIOs[resPath];
        }

        public long CopyEntryDataToStream(DOOMResourceEntry entry, Stream destStream, bool decompress = true)
        {
            var srcStream = entry.GetDataStream(decompress);
            if (srcStream == null)
                return 0;
            
            long copyLen = entry.CompressedSize;
            if (entry.IsCompressed && decompress)
                copyLen = entry.Size;

            return Utility.StreamCopy(destStream, srcStream, 40960, copyLen);
        }

        /*public static byte[] CompressData(byte[] data, ZLibNet.CompressionLevel level = ZLibNet.CompressionLevel.Level9)
        {
            using (var dest = new MemoryStream())
            {
                using (var source = new MemoryStream(data))
                {
                    using (var deflateStream = new ZLibNet.DeflateStream(dest, ZLibNet.CompressionMode.Compress, level, true))
                    {
                        source.CopyTo(deflateStream);

                        // DOOM's compressed resources all end with 00 00 FF FF
                        dest.SetLength(dest.Length + 4);
                        dest.Position = dest.Length - 2;
                        dest.WriteByte(0xFF);
                        dest.WriteByte(0xFF);

                        /* DOOM's compressed resources all seem to have the first bit unset
                         * tested by decompressing data and then recompressing using this Compress method, data is 1:1 except for the first bit (eg. our compressed data started with 0x7D, the games data would be 0x7C)
                         * in one test, keeping the bit set made the games graphics screw up and trying to open multiplayer would crash
                         * but unsetting this bit let the game work normally (using a slightly modified decl file also)
                         * another test like this using data that was heavily modded still resulted in a glitched game, even with this bit unset
                         * results inconclusive :( 
                        /*dest.Position = 0;
                        byte b = (byte)ms.ReadByte();
                        b &= byte.MaxValue ^ (1 << 0);
                        ms.Position = 0;
                        ms.WriteByte((byte)b);

                        return dest.ToArray();
                    }
                }
            }
        }*/

        private void addFilesFromFolder(string folder, string baseFolder, EndianIO destResources, ref List<string> addedFiles)
        {
            var dirs = Directory.GetDirectories(folder);
            var files = Directory.GetFiles(folder);
            foreach(var file in files)
            {
                if (addedFiles.Contains(Path.GetFullPath(file)))
                    continue;

                if (folder == baseFolder && Path.GetFileName(file).ToLower() == "fileids.txt")
                    continue; // don't want to add fileIds.txt from base

                var filePath = Path.GetFullPath(file).Substring(Path.GetFullPath(baseFolder).Length).Replace("\\", "/");
                var fileEntry = new DOOMResourceEntry(this);

                fileEntry.PatchFileNumber = PatchFileNumber;
                fileEntry.FileType = "file";
                if(filePath.Contains(";")) // fileType is specified
                {
                    var idx = filePath.IndexOf(";");
                    fileEntry.FileType = filePath.Substring(idx + 1);
                    filePath = filePath.Substring(0, idx);
                }
                fileEntry.FileName2 = filePath;
                fileEntry.FileName3 = filePath;

                bool needToPad = destResources.Stream.Length % 0x10 != 0;
                if (PatchFileNumber > 0 && destResources.Stream.Length == 4)
                    needToPad = false; // for some reason patch files start at 0x4 instead of 0x10

                if (needToPad)
                {
                    long numPadding = 0x10 - (destResources.Stream.Length % 0x10);
                    destResources.Stream.SetLength(destResources.Stream.Length + numPadding);
                }

                fileEntry.Offset = destResources.Stream.Length;

                byte[] fileData = File.ReadAllBytes(file);
                fileEntry.Size = fileEntry.CompressedSize = fileData.Length;

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
            if(PatchFileNumber > 0)
                header = new byte[]{ Header_Version, 0x53, 0x45, 0x52 }; // patch files start at 0x4 instead

            destResources.Writer.Write(header);

            var addedFiles = new List<string>();
            foreach (var file in Entries)
            {
                var replacePath = (String.IsNullOrEmpty(replaceFromFolder) ? String.Empty : Path.Combine(replaceFromFolder, file.GetFullName()));
                if (File.Exists(replacePath + ";" + file.FileType))
                    replacePath += ";" + file.FileType;

                bool isReplacing = !string.IsNullOrEmpty(replaceFromFolder) && File.Exists(replacePath);

                if (file.PatchFileNumber != PatchFileNumber && !isReplacing)
                    continue; // file is located in a different patch resource and we aren't replacing it, so skip it

                bool needToPad = destResources.Stream.Length % 0x10 != 0;
                if (PatchFileNumber > 0 && destResources.Stream.Length == 4)
                    needToPad = false; // for some reason patch files start at 0x4 instead of 0x10

                if (file.IsCompressed && !isReplacing && PatchFileNumber > 0) // compressed files not padded in patch files?
                    needToPad = false;

                if (needToPad)
                {
                    long numPadding = 0x10 - (destResources.Stream.Length % 0x10);
                    destResources.Stream.SetLength(destResources.Stream.Length + numPadding);
                }

                if (file.Size <= 0 && file.CompressedSize <= 0)
                {
                    // patch indexes have offsets for 0-byte files set to 0, but in normal indexes it's the current resource file length
                    file.Offset = PatchFileNumber > 0 ? 0 : destResources.Stream.Length;
                    continue;
                }

                var offset = destResources.Stream.Length;
                destResources.Stream.Position = offset;

                if (isReplacing)
                {
                    file.PatchFileNumber = PatchFileNumber;

                    addedFiles.Add(replacePath);
                    using (var fs = File.OpenRead(replacePath))
                        file.CompressedSize = file.Size = (int)Utility.StreamCopy(destResources.Stream, fs, 40960, fs.Length);
                }
                else
                    file.CompressedSize = (int)CopyEntryDataToStream(file, destResources.Stream, !keepCompressed);

                file.Offset = offset;
            }

            // now add any files that weren't replaced
            if(!String.IsNullOrEmpty(replaceFromFolder))
                addFilesFromFolder(replaceFromFolder, replaceFromFolder, destResources, ref addedFiles);

            // read the fileIds.txt file if it exists, and set the IDs
            var idFile = Path.Combine(replaceFromFolder, "fileIds.txt");
            if (File.Exists(idFile))
            {
                var lines = File.ReadAllLines(idFile);
                foreach(var line in lines)
                {
                    if (String.IsNullOrEmpty(line.Trim()))
                        continue;

                    var sepIdx = line.LastIndexOf('=');
                    if (sepIdx < 0)
                        continue; // todo: warn user?
                    var fileName = line.Substring(0, sepIdx).Trim();
                    var fileId = line.Substring(sepIdx + 1).Trim();
                    int id = -1;
                    if (!int.TryParse(fileId, out id))
                    {
                        Console.WriteLine($"Warning: file {fileName} defined in fileIds.txt but has invalid id!");
                        continue;
                    }

                    var file = Entries.Find(s => s.GetFullName() == fileName);
                    if (file != null)
                        file.ID = id;
                    else
                        Console.WriteLine($"Warning: file {fileName} defined in fileIds.txt but doesn't exist?");
                }
            }

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
            if(resourceIOs != null)
            {
                foreach (var kvp in resourceIOs)
                    kvp.Value.Close();

                resourceIOs.Clear();
                resourceIOs = null;
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
            var indexExt = Path.GetExtension(IndexFilePath);
            if (!File.Exists(IndexFilePath) || (indexExt != ".index" && indexExt != ".pindex"))
                return false; // not an index file

            if (!File.Exists(ResourceFilePath(0)))
                return false; // base resource data file not found!

            resourceIOs = new Dictionary<string, EndianIO>();

            indexIO = new EndianIO(IndexFilePath, FileMode.Open);

            indexIO.Stream.Position = 0;
            var magic = indexIO.Reader.ReadInt32();
            if ((magic & 0xFFFFFF00) != 0x52455300)
            {
                Close();
                return false; // not a RES file.
            }
            Header_Version = (byte)(magic & 0xFF);
            Header_IndexSize = indexIO.Reader.ReadInt32();

            // init the base resource data file
            if(GetResourceIO(0) == null)
            {
                Close();
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
                if (entry.PatchFileNumber > PatchFileNumber)
                    PatchFileNumber = entry.PatchFileNumber; // highest PatchFileNumber must be our patch file index
            }

            return true;
        }
    }
}
