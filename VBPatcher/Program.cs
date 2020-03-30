using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VBPatcher
{
    class Program
    {
        private static string Filepath { get; set; }

        private static FileStream file { get; set; }

        private static byte[] _source { get; set; }

        private static byte[] _pattern { get; set; }

        private static bool _32bit { get; set; }

        private static string FilePathToAppend { get; set; }

        private static int Patchsuccessnum { get; set; }

        private struct PatchInfo
        {
            public string Pattern;

            public string PatchBytes;

            public string PatchNumber;

            public int PatchOffset;

            public PatchInfo(string pattern, string patchBytes, string patchNumber, int patchOffset)
            {
                Pattern = pattern;
                PatchBytes = patchBytes;
                PatchNumber = patchNumber;
                PatchOffset = patchOffset;
            }
        }

        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Filepath = args[0];
                FilePathToAppend = args[0].Split(@"\")[^1];

                switch (PeArchitecture())
                {
                    case 0x010B:
                        _32bit = true;
                        break;
                    case 0x020B:
                        _32bit = false;
                        break;
                    default:
                        Console.WriteLine("Error, Could not get Binary Architecture from file, please specify the Architecture Yourself! [86/64]");

                        if (Console.ReadLine().Contains("86"))
                        {
                            _32bit = true;

                            Console.WriteLine("32-bit Mode Selected!");
                        }
                        else
                        {
                            _32bit = false;

                            Console.WriteLine("64-bit Mode Selected!");
                        }
                        break;
                }
            }
            else
                InitData();

            Console.WriteLine("Creating Backup...");

            if (CreateBackup())
            {
                Console.WriteLine("Backup Successfully Created");
            }
            else
            {
                Console.WriteLine("Error Creating Backup, still Patch Application? [y/n]");

                if (!Console.ReadLine().ToLower().Contains("y"))
                    return;
            }

            if (!OpenFileForReadAccess())
            {
                Console.WriteLine("Error while trying to Open the File!");

                Console.ReadLine();

                return;
            }

            SetUpSource();

            List<PatchInfo> lPInfo = GetPatchesForFileVersion();
            if (lPInfo == null || lPInfo.Count <= 0)
            {
                Console.WriteLine("Unknown Error while trying to get Patches!");

                Console.ReadLine();
                return;
            }

            foreach (PatchInfo pInfo in lPInfo)
            {
                PatternScanAndPatch(pInfo.Pattern, pInfo.PatchBytes, pInfo.PatchNumber, pInfo.PatchOffset);
            }

            if (Patchsuccessnum >= lPInfo.Count)
                Console.WriteLine($"\nAll { Patchsuccessnum } Patches were Successfull");
            else
                Console.WriteLine($"\nOnly { Patchsuccessnum } out of { lPInfo.Count } Patches were Successfull!");

            Console.ReadLine();
        }

        private static int PatternAt(byte[] source, byte[] pattern)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source.Skip(i).Take(pattern.Length).SequenceEqual(pattern))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int PatternAtPlaceHolder(byte[] source, byte[] pattern)
        {
            for (int i = 0; i < source.Length; i++)
            {
                bool nomatch = false;

                if (source[i] != pattern[0])
                    continue;
                for (int j = 0; j < pattern.Length && i + j < source.Length; j++)
                {
                    if (pattern[j] == 0x0)
                        continue;
                    if (pattern[j] != source[i + j])
                    {
                        nomatch = true;
                        break;
                    }
                }
                if (!nomatch)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void InitData()
        {

            Console.WriteLine("Patch x86(32-Bit Version) or x64(64-Bit Version)? [86/64]");

            if (Console.ReadLine().Contains("86"))
            {
                _32bit = true;
                Console.WriteLine("x86 Mode Selected");
            }
            else
            {
                _32bit = false;
                Console.WriteLine("x64 Mode Selected");
            }

            Console.WriteLine("Input VoiceMeeter Installation Directory \nExample: \"C:\\Program Files (x86)\\VB\\Voicemeeter\"");

            if (_32bit)
                FilePathToAppend = @"\voicemeeter8.exe";
            else
                FilePathToAppend = @"\voicemeeter8x64.exe";

            Filepath = Console.ReadLine() + FilePathToAppend;

        }

        private static bool CreateBackup()
        {
            try
            {
                File.Copy(Filepath, Filepath[0..^FilePathToAppend.Length] + FilePathToAppend + ".bkp");

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: { e.Message }");

                return false;
            }
        }

        private static bool OpenFileForReadAccess()
        {

            try
            {
                file = File.OpenRead(Filepath);
                return true;
            }

            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
                return false;
            }

        }

        private static void SetUpSource()
        {
            _source = new byte[file.Length];

            file.Read(_source, 0, (int)file.Length);

            file.Close();
        }

        private static void NewPattern(string pstring)
        {
            string[] pstringsplit = pstring.Split(" ");

            _pattern = new byte[pstringsplit.Length];

            for (int i = 0; i < pstringsplit.Length; i++)
            {
                byte value = Byte.Parse(pstringsplit[i], System.Globalization.NumberStyles.HexNumber);

                _pattern[i] = value;
            }
        }

        private static byte[] PatternConvert(string pattern)
        {
            List<byte> convertedArray = new List<byte>();
            foreach (string s in pattern.Split(" "))
            {
                if (s == "?")
                {
                    convertedArray.Add(0x0);
                }
                else
                {
                    convertedArray.Add(Convert.ToByte(s, 16));
                }
            }
            return convertedArray.ToArray();
        }

        private static int FindFileOffset()
        {
            return PatternAt(_source, _pattern);
        }

        private static int FindFileOffsetPlaceHolder()
        {
            return PatternAtPlaceHolder(_source, _pattern);
        }

        private static bool WritePatch(string PatchBytes, int FileOffset)
        {
            try
            {
                string[] patchsplit = PatchBytes.Split(" ");

                file = File.OpenWrite(Filepath);
                file.Seek(FileOffset, SeekOrigin.Begin);
                foreach (string patchbyte in patchsplit)
                {
                    byte _patchbyte = Byte.Parse(patchbyte, System.Globalization.NumberStyles.HexNumber);

                    file.WriteByte(_patchbyte);

                }
                file.Close();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: { e.Message }");
                return false;
            }
        }

        private static bool PatternScanAndPatch(string pattern, string patch, string patchnum, int offset)
        {

            int pat;
            if (pattern.Contains("?"))
            {
                _pattern = PatternConvert(pattern);

                pat = FindFileOffsetPlaceHolder();
            }
            else
            {
                NewPattern(pattern);
                pat = FindFileOffset();
            }

            if (pat > 0)
            {
                pat += offset;

                Console.WriteLine($"Valid Pattern Found at 0x{pat.ToString("X")}, Writing to File...");

                if (WritePatch(patch, pat))
                {
                    Console.WriteLine($"Patch { patchnum } at File Offset: 0x{ pat.ToString("X") } was Successfull");

                    Patchsuccessnum++;

                    return true;
                }
                else
                {
                    Console.WriteLine($"Error while trying to Patch the instruction at File Offset 0x{ pat.ToString("X") }!");
                }
            }
            else
            {
                Console.WriteLine($"Pattern = { pattern } not Found!");
            }

            return false;
        }

        private static ushort PeArchitecture()
        {
            ushort pearch = 0;

            if (!OpenFileForReadAccess())
                return 0;

            BinaryReader bReader = new BinaryReader(file);

            try
            {
                if (bReader.ReadUInt16() == 0x5A4D)
                {
                    file.Seek(0x3C, SeekOrigin.Begin);
                    file.Seek(bReader.ReadUInt32(), SeekOrigin.Begin);
                    if (bReader.ReadUInt32() == 0x4550)
                    {
                        file.Seek(20, SeekOrigin.Current);
                        pearch = bReader.ReadUInt16();
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: { e.Message }");
                bReader.Close();
                file.Close();

                return 0;
            }

            bReader.Close();
            file.Close();

            return pearch;
        }

        private static string GetFileVersion(string filepath)
        {
            return FileVersionInfo.GetVersionInfo(filepath).FileVersion;
        }

        private static List<PatchInfo> GetPatchesForFileVersion()
        {
            List<PatchInfo> patches = new List<PatchInfo>();

            string fileVersion = GetFileVersion(Filepath);

            if (fileVersion != null)
            {
                fileVersion = fileVersion.Replace(", ", ".");
                Console.WriteLine($"File Version: { fileVersion }");
            }
            else
                Console.WriteLine("No File Version Found!");

            if (_32bit)
            {
                switch (fileVersion)
                {
                    default: // Tested on File Version 3.0.1.0
                        Console.WriteLine(@"Using Patches for File Version 3.0.1.0 / 32-bit");
                        patches.Add(new PatchInfo("FF D6 ? ? ? ? ? ? ? ? ? ? ? ? ? ? E9", "EB 0C", "one", 2));
                        patches.Add(new PatchInfo("08 83 3D ? ? ? ? 00 0F 85", "90 E9", "two", 8));
                        patches.Add(new PatchInfo("81 FA ? ? ? ? 76", "EB", "three", 6));
                        patches.Add(new PatchInfo("F6 05 ? ? ? ? 1F 0F 85", "90 E9", "four", 7));
                        break;
                }
            }
            else
            {
                switch (fileVersion)
                {
                    default: // Tested on File Version 3.0.1.0
                        Console.WriteLine(@"Using Patches for File Version 3.0.1.0 / 64-bit");
                        patches.Add(new PatchInfo("B8 ? ? ? ? 48 8B CB FF 15 ? ? ? ? E9", "EB 04", "one", 8));
                        patches.Add(new PatchInfo("CE FF 15 ? ? ? ? 83 3D ? ? ? ? 00 0F 85", "90 E9", "two", 14));
                        patches.Add(new PatchInfo("76 ? FF 15 ? ? ? ? 45 33", "EB", "three", 0));
                        patches.Add(new PatchInfo("F6 05 ? ? ? ? 1F 0F 85", "90 E9", "four", 7));
                        break;
                }
            }
            return patches;
        }
    }
}