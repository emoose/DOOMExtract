using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DOOMExtract
{
    class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("Extraction: DOOMExtract.exe [pathToIndexFile] <destFolder>");
            Console.WriteLine("If destFolder isn't specified a folder will be created next to the index file.");
            Console.WriteLine("Files with fileType != \"file\" will have the fileType appended to the filename.");
            Console.WriteLine("eg. allowoverlays.decl;renderParm for fileType \"renderParm\"");
            Console.WriteLine();
            Console.WriteLine("Repacking: DOOMExtract.exe [pathToIndexFile] --repack [repackFolder]");
            Console.WriteLine("Will repack the resources with the files in the repack folder.");
            Console.WriteLine("Note that files that don't already exist in the resources will be added.");
            Console.WriteLine("To set a new files fileType append the fileType to its filename.");
            Console.WriteLine("eg. allowoverlays.decl;renderParm for fileType \"renderParm\"");
            Console.WriteLine();
            Console.WriteLine("Deleting files: DOOMExtract.exe [pathToIndexFile] --delete [file1] <file2> <file3> ...");
            Console.WriteLine("Will delete files from the resources package. Full filepaths should be specified.");
            Console.WriteLine("If a file isn't found in the package a warning will be given.");
        }
        static void Main(string[] args)
        {
            Console.WriteLine("DOOMExtract 1.4 - by infogram @ cs.rin.ru");
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

            if (args.Length >= 2)
            {
                if(args[1] == "--delete") // deleting
                {
                    isDeleting = true;
                }
                else if(args[1] == "--repack") // repacking
                {
                    isRepacking = true;
                    if (args.Length <= 2) // missing the repack folder arg
                    {
                        PrintUsage();
                        return;
                    }

                    destFolder = Path.GetFullPath(args[2]); // use destFolder as the folder where repack files are
                    if(!Directory.Exists(destFolder))
                    {
                        Console.WriteLine("Repack folder \"" + destFolder + "\" doesn't exist!");
                        return;
                    }
                }
                else
                    destFolder = Path.GetFullPath(args[1]);
            }

            Console.WriteLine("Loading " + indexFilePath + "...");
            var index = new DOOMResourceIndex(indexFilePath);
            if(!index.Load())
            {
                Console.WriteLine("Failed to load index file for some reason, is it a valid DOOM index file?");
                return;
            }

            Console.WriteLine("Index loaded, Header_NumEntries = " + index.Header_NumEntries.ToString());

            if(isRepacking)
            {
                // REPACK MODE!!!

                Console.WriteLine("Repacking/rebuilding resources file from folder " + destFolder + "...");
                index.Rebuild(index.ResourceFilePath + "_tmp", destFolder, true);
                index.Close();
                File.Delete(index.ResourceFilePath);
                File.Move(index.ResourceFilePath + "_tmp", index.ResourceFilePath);
                Console.WriteLine("Repack complete!");
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
                        Console.WriteLine("Failed to find file " + args[i] + " in package.");
                    else
                    {
                        index.Entries.RemoveAt(delIdx);
                        deleted++;
                        Console.WriteLine("Deleted " + args[i] + "!");
                    }
                }


                if (deleted > 0)
                {
                    Console.WriteLine("Repacking/rebuilding resources file...");
                    index.Rebuild(index.ResourceFilePath + "_tmp", String.Empty, true);
                    index.Close();
                    File.Delete(index.ResourceFilePath);
                    File.Move(index.ResourceFilePath + "_tmp", index.ResourceFilePath);
                }
                Console.WriteLine("Deleted " + deleted.ToString() + " files from resources.");
                return;
            }

            // EXTRACT MODE!

            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);

            int numExtracted = 0;
            var warned = new List<DOOMResourceEntry>();
            foreach(var entry in index.Entries)
            {
                if(entry.Size == 0 && entry.CompressedSize == 0) // blank entry?
                    continue;

                Console.WriteLine("Extracting " + entry.GetFullName() + " (type: " + entry.FileType + " size: " + entry.Size.ToString() + " compressed: " + entry.CompressedSize.ToString());

                var destFilePath = Path.Combine(destFolder, entry.GetFullName());
                if (entry.FileType != "file")
                    destFilePath += ";" + entry.FileType;

                var destFileFolder = Path.GetDirectoryName(destFilePath);

                var data = index.GetEntryData(entry);
                if(data.Length <= 0)
                {
                    Console.WriteLine("Decompression failed!");
                    continue;
                }

                if (data.Length != entry.Size)
                {
                    Console.WriteLine("WARNING: Decompression resulted in " + data.Length + " bytes, but we expected " + entry.Size + " bytes!");
                    warned.Add(entry);
                }

                if (!Directory.Exists(destFileFolder))
                    Directory.CreateDirectory(destFileFolder);

                File.WriteAllBytes(destFilePath, data);
                Console.WriteLine("----------------------------------------------------");
                numExtracted++;
            }

            Console.WriteLine("Extraction complete! Extracted " + numExtracted.ToString() + " files.");
            if (warned.Count > 0)
                Console.WriteLine("... with " + warned.Count + " warnings!");
        }
    }
}
