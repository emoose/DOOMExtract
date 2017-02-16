using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace DOOMExtract
{
    class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("Extraction: DOOMExtract.exe [pathToIndexFile] <destFolder>");
            Console.WriteLine("  If destFolder isn't specified a folder will be created next to the index/pindex file.");
            Console.WriteLine("  Files with fileType != \"file\" will have the fileType appended to the filename.");
            Console.WriteLine("  eg. allowoverlays.decl;renderParm for fileType \"renderParm\"");
            Console.WriteLine("  A list of each files ID will be written to [destFolder]\\fileIds.txt");
            Console.WriteLine();
            Console.WriteLine("Repacking: DOOMExtract.exe [pathToIndexFile] --repack [repackFolder]");
            Console.WriteLine("  Will repack the resources with the files in the repack folder.");
            Console.WriteLine("  Files that don't already exist in the resources will be added.");
            Console.WriteLine("  To set a new files fileType append the fileType to its filename.");
            Console.WriteLine("  eg. allowoverlays.decl;renderParm for fileType \"renderParm\"");
            Console.WriteLine("  To set/change a files ID, add a line for it in [repackFolder]\\filesIds.txt");
            Console.WriteLine("  of the format [full file path]=[file id]");
            Console.WriteLine("  eg. \"example\\test.txt=1337\"");
            Console.WriteLine("  (Note that you should only rebuild the latest patch index file,");
            Console.WriteLine("  as patches rely on the data in earlier files!)");
            Console.WriteLine();
            Console.WriteLine("Patch creation: DOOMExtract.exe [pathToLatestPatchIndex] --createPatch [patchContentsFolder]");
            Console.WriteLine("  Allows you to create your own patch files.");
            Console.WriteLine("  Works like repacking above, but the resulting patch files will");
            Console.WriteLine("  only contain the files you've added/changed.");
            Console.WriteLine("  Make sure to point it to the highest-numbered .pindex file!");
            Console.WriteLine("  Once completed a new .patch/.pindex file pair should be created.");
            Console.WriteLine();
            Console.WriteLine("Deleting files: DOOMExtract.exe [pathToIndexFile] --delete [file1] <file2> <file3> ...");
            Console.WriteLine("  Will delete files from the resources package. Full filepaths should be specified.");
            Console.WriteLine("  If a file isn't found in the package a warning will be given.");
            Console.WriteLine("  This should only be used on the latest patch file, as modifying");
            Console.WriteLine("  earlier patch files may break later ones.");
        }
        static void Main(string[] args)
        {
            Console.WriteLine("DOOMExtract 1.7 by infogram - https://github.com/emoose/DOOMExtract");
            Console.WriteLine();
            if (args.Length <= 0)
            {
                PrintUsage();
                return;
            }

            string indexFilePath = args[0];
            string destFolder = Path.GetDirectoryName(indexFilePath);
            destFolder = Path.Combine(destFolder, Path.GetFileNameWithoutExtension(indexFilePath));

            bool isRepacking = false;
            bool isDeleting = false;
            bool isCreatingPatch = false;
            bool quietMode = false;

            foreach (var arg in args)
                if (arg == "--quiet")
                    quietMode = true;

            if (args.Length >= 2)
            {
                if(args[1] == "--delete") // deleting
                {
                    isDeleting = true;
                }
                else if(args[1] == "--repack" || args[1] == "--createPatch") // repacking
                {
                    isRepacking = true;
                    isCreatingPatch = args[1] == "--createPatch";

                    if (args.Length <= 2) // missing the repack folder arg
                    {
                        PrintUsage();
                        return;
                    }

                    destFolder = Path.GetFullPath(args[2]); // use destFolder as the folder where repack files are
                    if(!Directory.Exists(destFolder))
                    {
                        Console.WriteLine((isCreatingPatch ? "Patch" : "Repack") + $" folder \"{destFolder}\" doesn't exist!");
                        return;
                    }
                }
                else
                    destFolder = Path.GetFullPath(args[1]);
            }

            Console.WriteLine($"Loading {indexFilePath}...");
            var index = new DOOMResourceIndex(indexFilePath);
            if(!index.Load())
            {
                Console.WriteLine("Failed to load index file for some reason, is it a valid DOOM index file?");
                return;
            }

            Console.WriteLine($"Index loaded ({index.Entries.Count} files)" + (!quietMode ? ", data file contents:" : ""));

            if (!quietMode)
            {
                var pfis = new Dictionary<int, int>();

                foreach (var entry in index.Entries)
                {
                    if (!pfis.ContainsKey(entry.PatchFileNumber))
                        pfis.Add(entry.PatchFileNumber, 0);
                    pfis[entry.PatchFileNumber]++;
                }

                var pfiKeys = pfis.Keys.ToList();
                pfiKeys.Sort();

                int total = 0;
                foreach (var key in pfiKeys)
                {
                    var resName = Path.GetFileName(index.ResourceFilePath(key));
                    Console.WriteLine($"  {resName}: {pfis[key]} files");
                    total += pfis[key];
                }

                Console.WriteLine();
            }

            if (isCreatingPatch)
            {
                // clone old index and increment the patch file number

                byte pfi = (byte)(index.PatchFileNumber + 1);
                var destPath = Path.ChangeExtension(index.ResourceFilePath(pfi), ".pindex");
                index.Close();

                if (File.Exists(destPath))
                    File.Delete(destPath); // !!!!

                File.Copy(indexFilePath, destPath);
                indexFilePath = destPath;

                index = new DOOMResourceIndex(destPath);
                if(!index.Load())
                {
                    Console.WriteLine("Copied patch file failed to load? (this should never happen!)");
                    return;
                }
                index.PatchFileNumber = pfi;
            }

            if(isRepacking)
            {
                // REPACK (and patch creation) MODE!!!

                var resFile = index.ResourceFilePath(index.PatchFileNumber);
                
                Console.WriteLine((isCreatingPatch ? "Creating" : "Repacking") + $" {Path.GetFileName(indexFilePath)} from folder {destFolder}...");

                index.Rebuild(resFile + "_tmp", destFolder, true);
                index.Close();
                if (!File.Exists(resFile + "_tmp"))
                {
                    Console.WriteLine("Failed to create new resource data file!");
                    return;
                }

                if (File.Exists(resFile))
                    File.Delete(resFile);

                File.Move(resFile + "_tmp", resFile);
                Console.WriteLine(isCreatingPatch ? "Patch file created!" : "Repack complete!");
                return;
            }

            if(isDeleting)
            {
                if(args.Length <= 2)
                {
                    PrintUsage();
                    return;
                }

                // DELETE MODE!!
                int deleted = 0;
                for(int i = 2; i < args.Length; i++)
                {
                    var path = args[i].Replace("/", "\\").ToLower();

                    int delIdx = -1;
                    for(int j = 0; j < index.Entries.Count; j++)
                    {
                        if (index.Entries[j].GetFullName().ToLower() == path)
                        {
                            delIdx = j;
                            break;
                        }
                    }

                    if (delIdx == -1)
                        Console.WriteLine($"Failed to find file {args[i]} in package.");
                    else
                    {
                        index.Entries.RemoveAt(delIdx);
                        deleted++;
                        Console.WriteLine($"Deleted {args[i]}!");
                    }
                }


                if (deleted > 0)
                {
                    Console.WriteLine("Repacking/rebuilding resources file...");
                    index.Rebuild(index.ResourceFilePath(index.PatchFileNumber) + "_tmp", String.Empty, true);
                    index.Close();
                    File.Delete(index.ResourceFilePath(index.PatchFileNumber));
                    File.Move(index.ResourceFilePath(index.PatchFileNumber) + "_tmp", index.ResourceFilePath(index.PatchFileNumber));
                }
                Console.WriteLine($"Deleted {deleted} files from resources.");
                return;
            }

            // EXTRACT MODE!

            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);

            Console.WriteLine("Extracting contents to:");
            Console.WriteLine("\t" + destFolder);

            var fileIds = new List<string>();
            int numExtracted = 0;
            int numProcessed = 0;
            foreach(var entry in index.Entries)
            {
                numProcessed++;
                if(entry.Size == 0 && entry.CompressedSize == 0) // blank entry?
                    continue;
                
                Console.WriteLine($"Extracting {entry.GetFullName()}...");
                Console.WriteLine($"    id: {entry.ID}, type: {entry.FileType}, size: {entry.Size} ({entry.CompressedSize} bytes compressed)");
                Console.WriteLine($"    source: {Path.GetFileName(index.ResourceFilePath(entry.PatchFileNumber))}");

                var destFilePath = Path.Combine(destFolder, entry.GetFullName());
                if (entry.FileType != "file")
                    destFilePath += ";" + entry.FileType;

                var destFileFolder = Path.GetDirectoryName(destFilePath);

                if (!Directory.Exists(destFileFolder))
                    Directory.CreateDirectory(destFileFolder);

                using (FileStream fs = File.OpenWrite(destFilePath))
                    index.CopyEntryDataToStream(entry, fs);
                
                Console.WriteLine($"--------------({numProcessed}/{index.Entries.Count})--------------");
                fileIds.Add(entry.GetFullName() + "=" + entry.ID);
                numExtracted++;
            }

            if (fileIds.Count > 0)
            {
                var idFile = Path.Combine(destFolder, "fileIds.txt");
                if (File.Exists(idFile))
                    File.Delete(idFile);
                File.WriteAllLines(idFile, fileIds.ToArray());
            }

            Console.WriteLine($"Extraction complete! Extracted {numExtracted} files.");
        }
    }
}
