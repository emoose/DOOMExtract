using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using DOOMExtract;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace DOOMModLoader
{
    class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("Usage: DOOMModLoader.exe [-mp/-snap] [-vulkan] [-moddir <pathToModsFolder>]");
            Console.WriteLine();
            Console.WriteLine("  -mp\t\t\tLoad MP gamemode");
            Console.WriteLine("  -snap\t\t\tLoad SnapMap gamemode");
            Console.WriteLine("  -vulkan\t\tLoad DOOMx64vk instead of DOOMx64");
            Console.WriteLine("  -moddir <path>\tUse mods from given path");
            Console.WriteLine("  -help\t\t\tDisplay this text");
        }

        static void Main(string[] args)
        {
            string gameMode = "1";
            string resourcePrefix = "";
            string modDir = "mods";
            string exeName = "DOOMx64.exe";

            Console.WriteLine("DOOMModLoader 0.2 by infogram - https://github.com/emoose/DOOMExtract");
            Console.WriteLine();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "-mp":
                    case "/mp":
                        gameMode = "2";
                        resourcePrefix = "mp_";
                        break;
                    case "-snap":
                    case "/snap":
                        gameMode = "3";
                        resourcePrefix = "snap_";
                        break;
                    case "-moddir":
                    case "/moddir":
                        if ((i + 1) < args.Length)
                            modDir = args[i + 1];
                        break;
                    case "-vulkan":
                    case "/vulkan":
                        exeName = "DOOMx64vk.exe";
                        break;
                    case "-help":
                    case "/help":
                        PrintUsage();
                        return;
                }
            }

            if (!File.Exists(exeName))
            {
                Console.WriteLine($"Error: failed to find {exeName} in current directory!");
                PressKeyPrompt();
                return;
            }

            if (!Directory.Exists(modDir))
                Directory.CreateDirectory(modDir);

            bool hasMods = false;

            // create folder to extract mods into
            var extractedPath = Path.GetTempFileName();
            File.Delete(extractedPath);
            Directory.CreateDirectory(extractedPath);

            // copy loose mods from modDir into extract path
            Console.WriteLine("Extracting/copying mods into " + extractedPath);

            var modFiles = Directory.GetFiles(modDir);
            var modDirs = Directory.GetDirectories(modDir);

            var zips = new List<string>();
            foreach(var file in modFiles)
            {
                hasMods = true;

                if (Path.GetExtension(file).ToLower() == ".zip")
                    zips.Add(file); // don't copy zips, we'll extract them later instead
                else
                {
                    File.Copy(file, Path.Combine(extractedPath, Path.GetFileName(file)));
                }
            }
            foreach (var dir in modDirs)
            {
                hasMods = true;
                CloneDirectory(dir, Path.Combine(extractedPath, Path.GetFileName(dir)));
            }

            // extract mod zips
            var modInfoPath = Path.Combine(extractedPath, "modinfo.txt");
            var fileIdsPath = Path.Combine(extractedPath, "fileIds.txt");
            var fileIds = "";
            if (File.Exists(fileIdsPath))
            {
                fileIds = File.ReadAllText(fileIdsPath);
                File.Delete(fileIdsPath);
            }

            foreach (var zipfile in zips)
            {
                var modInfo = Path.GetFileName(zipfile);
                Console.WriteLine("Extracting " + modInfo);
                ExtractZipFile(zipfile, "", extractedPath);
                if (File.Exists(modInfoPath))
                {
                    modInfo = File.ReadAllText(modInfoPath);
                    if (String.IsNullOrEmpty(modInfo))
                        modInfo = Path.GetFileName(zipfile);

                    File.Delete(modInfoPath); // delete so no conflicts
                }
                if (File.Exists(fileIdsPath))
                {
                    // todo: make this use a dictionary instead, so we can detect conflicts
                    var modsFileIds = File.ReadAllText(fileIdsPath);
                    if (!String.IsNullOrEmpty(fileIds))
                        fileIds += Environment.NewLine;
                    fileIds += modsFileIds;

                    File.Delete(fileIdsPath);
                }

                Console.WriteLine("Extracted " + modInfo);
            }
            if (!String.IsNullOrEmpty(fileIds))
                File.WriteAllText(fileIdsPath, fileIds);

            // mod patch creation
            var patchFilter = $"{resourcePrefix}gameresources_*.pindex";
            var patches = Directory.GetFiles("base", patchFilter);
            var latestPatch = String.Empty;
            var latestPfi = 0;
            foreach (var patch in patches)
            {
                if (File.Exists(patch + ".custom"))
                    continue; // patch is one made by us

                var namesp = Path.GetFileNameWithoutExtension(patch).Split('_');
                var pnum = int.Parse(namesp[namesp.Length - 1]);
                if (pnum > latestPfi)
                {
                    latestPatch = patch;
                    latestPfi = pnum;
                }
            }
            if (string.IsNullOrEmpty(latestPatch))
            {
                Console.WriteLine("Failed to find latest patch file in base folder!");
                Console.WriteLine($"Search filter: {patchFilter}");
                PressKeyPrompt();
                return;
            }

            var customPfi = latestPfi + 1;

            // have to find where to copy the index, easiest way is to make a DOOMResourceIndex instance (but not load it)
            var index = new DOOMResourceIndex(latestPatch);
            var resPath = index.ResourceFilePath(customPfi);
            var destPath = Path.ChangeExtension(resPath, ".pindex");

            // delete existing custom patch
            if (File.Exists(destPath))
                File.Delete(destPath);
            
            if (File.Exists(resPath))
                File.Delete(resPath);

            // if we have mods, create a custom patch out of them
            if (hasMods)
            {
                File.Copy(latestPatch, destPath);

                Console.WriteLine($"Creating custom patch... (patch base: {Path.GetFileName(latestPatch)})");

                index = new DOOMResourceIndex(destPath);
                if (!index.Load())
                {
                    Console.WriteLine("Failed to load custom patch " + destPath);
                    PressKeyPrompt();
                    return;
                }
                index.PatchFileNumber = (byte)customPfi;

                index.Rebuild(index.ResourceFilePath(customPfi), extractedPath, true);
                index.Close();

                File.WriteAllText(destPath + ".custom", "DOOMModLoader token file, this tells ModLoader that this is a custom patch file, please don't remove!");

                Console.WriteLine($"Custom patch {Path.GetFileName(destPath)} created.");
            }

            // cleanup
            Directory.Delete(extractedPath, true);

            Console.WriteLine("Launching game!");

            var proc = new Process();
            proc.StartInfo.FileName = exeName;
            proc.StartInfo.Arguments = $"+com_gameMode {gameMode} +com_restarted 1 +devMode_enable 1";
            proc.Start();
        }

        static void PressKeyPrompt()
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void CloneDirectory(string src, string dest)
        {
            if (!Directory.Exists(dest))
                Directory.CreateDirectory(dest);

            foreach (var directory in Directory.GetDirectories(src))
            {
                string dirName = Path.GetFileName(directory);
                if (!Directory.Exists(Path.Combine(dest, dirName)))
                {
                    Directory.CreateDirectory(Path.Combine(dest, dirName));
                }
                CloneDirectory(directory, Path.Combine(dest, dirName));
            }

            foreach (var file in Directory.GetFiles(src))
            {
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
            }
        }

        static void ExtractZipFile(string archiveFilenameIn, string password, string outFolder)
        {
            ZipFile zf = null;
            try
            {
                FileStream fs = File.OpenRead(archiveFilenameIn);
                zf = new ZipFile(fs);
                if (!string.IsNullOrEmpty(password))
                    zf.Password = password;     // AES encrypted entries are handled automatically

                foreach (ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile)
                        continue;           // Ignore directories

                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    byte[] buffer = new byte[4096];     // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    // Manipulate the output filename here as desired.
                    string fullZipToPath = Path.Combine(outFolder, zipEntry.Name);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    if (File.Exists(fullZipToPath))
                        File.Delete(fullZipToPath); // TODO: warn user about mod conflict!

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.

                    using (FileStream streamWriter = File.Create(fullZipToPath))
                    {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            }
            finally
            {
                if (zf != null)
                {
                    zf.IsStreamOwner = true; // Makes close also shut the underlying stream
                    zf.Close(); // Ensure we release resources
                }
            }
        }
    }
}
