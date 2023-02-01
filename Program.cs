using System.IO.Compression;
using System.Security.Cryptography;

namespace IncrementalUpdatePackMaker;

internal abstract class Program
{
    private static void CreateCompressedFile(string fileOrDir, string zipFileName)
    {
        if (File.Exists(zipFileName)) MyDelete(zipFileName);
        ZipFile.CreateFromDirectory(fileOrDir, zipFileName);
    }

    private static string GetFileHash(string fileString)
    {
        HashAlgorithm hashAlgorithm = MD5.Create();
        if (!File.Exists(fileString)) return string.Empty;
        FileStream fileStream = new(fileString, FileMode.Open, FileAccess.Read);
        string res = BitConverter.ToString(hashAlgorithm.ComputeHash(fileStream)).Replace("-", "").ToUpper();
        fileStream.Close();
        return res;
    }

    private static void InputPathInf(out string oldFilePath2, out string newFilePath2, out string outputPath2)
    {
        {
            StartInput:
            Console.WriteLine(@"Input folder path for last updated files:(DEFAULT: D:\LastUpdated\)");
            string? oldFilePath = Console.ReadLine();
            oldFilePath = Path.GetFullPath(oldFilePath is "" or null ? @"D:\LastUpdated\" : oldFilePath);
            if (!Directory.Exists(oldFilePath))
            {
                Console.WriteLine("Folder not Exist! Retry.");
                goto StartInput;
            }

            Console.WriteLine("");
            Console.WriteLine(@"Input folder path for New Downloaded files:(DEFAULT: D:\NewDownloaded\)");
            string? newFilePath = Console.ReadLine();
            newFilePath = Path.GetFullPath(newFilePath is "" or null ? @"D:\NewDownloaded\" : newFilePath);
            if (!Directory.Exists(newFilePath))
            {
                Console.WriteLine("Folder not Exist! Retry.");
                goto StartInput;
            }

            Console.WriteLine("");
            Console.WriteLine(@"Input folder path for Output files:(DEFAULT: D:\UpdatePack\)");
            string? outputPath = Console.ReadLine();
            outputPath = Path.GetFullPath(outputPath is "" or null ? @"D:\UpdatePack\" : outputPath);
            if (!Directory.Exists(outputPath))
            {
                Console.WriteLine("Folder not Exist! Created.");
                Directory.CreateDirectory(outputPath);
            }

            Console.WriteLine("");
            Console.WriteLine("Old Files Folder: " + oldFilePath);
            Console.WriteLine("New Files Folder: " + newFilePath);
            Console.WriteLine("Output Files Folder: " + outputPath);
            while (true)
            {
                Console.WriteLine("");
                Console.WriteLine("Confirm?(Y/N)");
                string? rs = Console.ReadLine();
                if (rs == "Y")
                    break;
                if (rs == "N")
                    goto StartInput;
                Console.WriteLine("Input Error!");
            }

            oldFilePath2 = oldFilePath;
            newFilePath2 = newFilePath;
            outputPath2 = outputPath;
        }
    }

    private static (List<string>, List<string>) CompareFolder(string oldFilePath, string newFilePath)
    {
        List<string> neededCreateFiles = new();
        List<string> neededUpdateFiles = new();
        List<string> neededDeleteFiles = new();
        DirectoryInfo oldDirectoryInfo = new(oldFilePath);
        FileInfo[] oldFileInfos = oldDirectoryInfo.GetFiles("*.*", SearchOption.AllDirectories);
        DirectoryInfo newDirectoryInfo = new(newFilePath);
        FileInfo[] newFileInfos = newDirectoryInfo.GetFiles("*.*", SearchOption.AllDirectories);
        Console.WriteLine("");
        Console.WriteLine("Analyzing. Wait.");
        foreach (FileInfo file in oldFileInfos)
        {
            string fullpaths = file.FullName;
            string relativityPath = file.FullName.Replace(oldFilePath, "");
            fullpaths = fullpaths.Replace(oldFilePath, newFilePath);
            if (!File.Exists(fullpaths)) neededDeleteFiles.Add(relativityPath);
        }

        foreach (FileInfo file in newFileInfos)
        {
            string fullpaths = file.FullName;
            string relativityPath = file.FullName.Replace(newFilePath, "");
            fullpaths = fullpaths.Replace(newFilePath, oldFilePath);
            if (!File.Exists(fullpaths))
            {
                neededCreateFiles.Add(relativityPath);
            }
            else
            {
                FileInfo fileInfo = new(fullpaths);
                if (GetFileHash(file.FullName) == GetFileHash(fileInfo.FullName)) continue;
                neededUpdateFiles.Add(relativityPath);
                neededDeleteFiles.Add(relativityPath);
                neededCreateFiles.Add(relativityPath);
            }
        }

        Console.WriteLine("");
        Console.WriteLine("Analysis Finished. ");
        Console.WriteLine("There are " + neededCreateFiles.Count + " Item(s) need to be Created. " +
                          neededUpdateFiles.Count + " Item(s) need to be Updated. " + neededDeleteFiles.Count +
                          " Item(s) need to be Deleted. ");
        return (neededCreateFiles, neededDeleteFiles);
    }

    private static void MyDelete(string path)
    {
        if (!Directory.Exists(path)) return;
        DirectoryInfo directoryInfo = new(path);
        if (directoryInfo.GetDirectories().Length == 0 && directoryInfo.GetFiles().Length == 0)
        {
            directoryInfo.Delete();
            return;
        }

        foreach (DirectoryInfo d in directoryInfo.GetDirectories()) MyDelete(d.FullName);
        foreach (FileInfo fileInfo in directoryInfo.GetFiles()) fileInfo.Delete();
        directoryInfo.Delete();
    }

    private static void MakeUpdatePackage(List<string> neededCreateFiles, List<string> neededDeleteFiles,
        string oldFilePath,
        string newFilePath, string outputPath)
    {
        Console.WriteLine("");
        if (neededCreateFiles.Count == 0 && neededDeleteFiles.Count == 0)
        {
            Console.WriteLine("No need to Update,you have the latest version.");
            return;
        }

        Console.WriteLine("Making Update Package.Wait.");
        string tempPath = Path.Combine(outputPath, "temp");
        if (Directory.Exists(tempPath)) MyDelete(tempPath);
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(Path.Combine(tempPath, "NewFile"));
        string tempNewFile = Path.Combine(tempPath, "NewFile");
        foreach (string repath in neededCreateFiles)
        {
            string source = Path.GetFullPath(newFilePath + repath);
            string destination = Path.GetFullPath(tempNewFile + repath);
            string? directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(directory)) return;
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.Copy(source, destination);
        }

        FileStream fileStream =
            new(Path.Combine(tempPath, "DeleteFile.blist"), FileMode.OpenOrCreate, FileAccess.Write);
        File.WriteAllText(Path.Combine(tempPath, "DeleteFile.nlist"), neededDeleteFiles.Count.ToString());
        BinaryWriter binaryWriter = new(fileStream);
        MemoryStream ms = new();
        BinaryWriter memoryBinaryWriter = new(ms);
        foreach (string s in neededDeleteFiles) memoryBinaryWriter.Write(s);
        memoryBinaryWriter.Close();
        byte[] bytes = ms.ToArray();
        string b64 = Convert.ToBase64String(bytes);
        binaryWriter.Write(b64);
        binaryWriter.Close();
        fileStream.Close();

        //Self test.
        int count = Convert.ToInt32(File.ReadAllText(Path.Combine(tempPath, "DeleteFile.nlist")));
        FileStream fileStreamTest = new(Path.Combine(tempPath, "DeleteFile.blist"), FileMode.Open, FileAccess.Read);
        BinaryReader binaryReader = new(fileStreamTest);
        string readString64 = binaryReader.ReadString();
        byte[] buffer = Convert.FromBase64String(readString64);
        List<string> files = new();
        BinaryReader memReader = new(new MemoryStream(buffer));
        for (int i = 0; i < count; i++) files.Add(memReader.ReadString());
        binaryReader.Close();
        memReader.Close();
        fileStreamTest.Close();
        bool res = true;
        for (int i = 0; i < files.Count; i++)
            if (string.CompareOrdinal(files[i], neededDeleteFiles[i]) != 0)
                res = false;
        if (res)
        {
            Console.WriteLine("Test OK.Start Compressing.");
        }
        else
        {
            MyDelete(tempPath);
            Console.WriteLine("");
            Console.WriteLine("Test Error.Exit.");
            return;
        }

        try
        {
            if (File.Exists(Path.Combine(outputPath, "UPDPACK.zip")))
                File.Delete(Path.Combine(outputPath, "UPDPACK.zip"));
            CreateCompressedFile(tempPath, Path.Combine(outputPath, "UPDPACK.zip"));
        }
        catch
        {
            MyDelete(tempPath);
            return;
        }

        MyDelete(tempPath);
        Console.WriteLine("");
        Console.WriteLine("Succeed. Package is Here: " + Path.Combine(outputPath, "UPDPACK.zip"));
        Console.WriteLine("");
        Console.WriteLine("Do you want to update your local old file folder?(Y/N)");
        if (Console.ReadLine() == "Y") Deploy(Path.Combine(outputPath, "UPDPACK.zip"), oldFilePath);
        Console.WriteLine("");
        Console.WriteLine("Everything is OK.");
        Console.WriteLine("");
    }

    private static bool MakeVerify(string oldFilePath, string newFilePath, string outputPath)
    {
        if (!Directory.Exists(oldFilePath)) return false;
        if (!Directory.Exists(newFilePath)) return false;
        if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);
        return true;
    }

    private static bool DeployVerify(string newFilePath, string outputPath)
    {
        return File.Exists(newFilePath) && Directory.Exists(outputPath);
    }

    private static void Main(string[] args)
    {
        switch (args.Length)
        {
            case 0:
                NoArgs();
                break;
            case 3:
                if (args[0] != "deploy") goto default;
                if (!DeployVerify(args[1], args[2])) goto default;
                ArgsDeploy(args);
                break;
            case 4:
                if (args[0] != "make") goto default;
                if (!MakeVerify(args[1], args[2], args[3])) goto default;
                ArgsMake(args);
                break;
            default:
                Console.WriteLine("                ");
                Console.WriteLine("       When you see this means you input wrong instruction.");
                Console.WriteLine("                ");
                Console.WriteLine(
                    "Usage: EXE_NAME make [DIR|oldUpdatedFileFolder] [DIR|newDownloadedFileFolder] [DIR|UpdatePakOutputFolder] ");
                Console.WriteLine("       EXE_NAME deploy [FILE|UpdatePackFullPathName] [DIR|FolderPathNeedUpdate] ");
                Console.WriteLine("                ");
                Console.WriteLine(
                    "Tips:  All Directory/File in the Parameter must exist in the filesystem and reachable,not locked.");
                Console.WriteLine("                ");
                return;
        }
    }

    private static void NoArgs()
    {
        InputPathInf(out string oldFilePath, out string newFilePath, out string outputPath);
        (List<string> neededCreateFiles, List<string> neededDeleteFiles) = CompareFolder(oldFilePath, newFilePath);
        MakeUpdatePackage(neededCreateFiles, neededDeleteFiles, oldFilePath, newFilePath, outputPath);
    }

    private static void ArgsMake(IReadOnlyList<string> args)
    {
        string oldFilePath = args[1];
        string newFilePath = args[2];
        string outputPath = args[3];
        (List<string> neededCreateFiles, List<string> neededDeleteFiles) = CompareFolder(oldFilePath, newFilePath);
        MakeUpdatePackage(neededCreateFiles, neededDeleteFiles, oldFilePath, newFilePath, outputPath);
    }

    private static void ArgsDeploy(IReadOnlyList<string> args)
    {
        Console.WriteLine("");
        string updateFile = args[1];
        string newPath = args[2];
        string tempPath = Path.Combine(Environment.CurrentDirectory, "temp");
        string tempNewFilePath = Path.Combine(tempPath, "NewFile");
        if (!File.Exists(updateFile))
        {
            Console.WriteLine(updateFile + " not found.");
            Console.WriteLine("Update package not exist. Check your input.");
            return;
        }

        Console.WriteLine("Update package Detected. Checking your Update.");
        if (Directory.Exists(tempPath)) MyDelete(tempPath);
        Directory.CreateDirectory(tempPath);
        ZipFile.ExtractToDirectory(updateFile, tempPath);
        int count = Convert.ToInt32(File.ReadAllText(Path.Combine(tempPath, "DeleteFile.nlist")));
        FileStream fileStreamTest = new(Path.Combine(tempPath, "DeleteFile.blist"), FileMode.Open, FileAccess.Read);
        BinaryReader binaryReader = new(fileStreamTest);
        string readString64 = binaryReader.ReadString();
        byte[] buffer = Convert.FromBase64String(readString64);
        BinaryReader memReader = new(new MemoryStream(buffer));
        List<string> filesToDelete = new();
        for (int i = 0; i < count; i++) filesToDelete.Add(memReader.ReadString());
        binaryReader.Close();
        memReader.Close();
        fileStreamTest.Close();
        foreach (string toDelete in filesToDelete)
        {
            string tpp = Path.GetFullPath(newPath + toDelete);
            if (!File.Exists(tpp))
            {
                Console.WriteLine("Some files should exist in your computer not found.");
                Console.WriteLine("Wrong Version of update package. You missed something when make this package.");
                Console.WriteLine("No file has been changed in current computer.");
                return;
            }
        }

        DirectoryInfo directoryInfo = new(tempNewFilePath);
        FileInfo[] fileInfos = directoryInfo.GetFiles("*.*", SearchOption.AllDirectories);
        List<string> needToCreateFiles = new();
        foreach (FileInfo newFile in fileInfos)
        {
            string fullpaths = newFile.FullName;
            string relativityPath = fullpaths.Replace(tempNewFilePath, "");
            fullpaths = Path.GetFullPath(newPath + relativityPath);
            if (!File.Exists(fullpaths))
            {
                needToCreateFiles.Add(relativityPath);
            }
            else
            {
                Console.WriteLine("Some files should not exist in your computer was found.");
                Console.WriteLine("Wrong Version of update package. You missed something when make this package.");
                Console.WriteLine("No file has been changed in current computer.");
                return;
            }
        }


        Console.WriteLine("Update package Check passed. Start Updating.");
        foreach (string toDelete in filesToDelete)
        {
            string tpp = Path.GetFullPath(newPath + toDelete);
            if (File.Exists(tpp))
            {
                File.Delete(tpp);
                Console.WriteLine("Delete: " + tpp);
            }
            else
            {
                Console.WriteLine("You are kidding me.");
                return;
            }
        }

        foreach (string createFile in needToCreateFiles)
        {
            string source = Path.GetFullPath(tempNewFilePath + createFile);
            string destination = Path.GetFullPath(newPath + createFile);
            string? directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(directory))
            {
                Console.WriteLine("Something wrong happened.");
                Console.WriteLine("No file has been changed in current computer.");
                return;
            }

            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.Copy(source, destination);
            Console.WriteLine("Copy: " + destination);
        }

        MyDelete(tempPath);
        Console.WriteLine("Update finished. Do you want to delete the Update package " + updateFile + " ?(Y/N)");
        if (Console.ReadLine() == "Y") File.Delete(updateFile);
    }

    private static void Deploy(string updateFile, string newPath)
    {
        Console.WriteLine("");
        Console.WriteLine("Analyzing.");
        string tempPath = Path.Combine(Environment.CurrentDirectory, "temp");
        string tempNewFilePath = Path.Combine(tempPath, "NewFile");
        if (!File.Exists(updateFile))
        {
            Console.WriteLine("Error");
            return;
        }

        if (Directory.Exists(tempPath)) MyDelete(tempPath);
        Directory.CreateDirectory(tempPath);
        ZipFile.ExtractToDirectory(updateFile, tempPath);
        int count = Convert.ToInt32(File.ReadAllText(Path.Combine(tempPath, "DeleteFile.nlist")));
        FileStream fileStreamTest = new(Path.Combine(tempPath, "DeleteFile.blist"), FileMode.Open, FileAccess.Read);
        BinaryReader binaryReader = new(fileStreamTest);
        string readString64 = binaryReader.ReadString();
        byte[] buffer = Convert.FromBase64String(readString64);
        BinaryReader memReader = new(new MemoryStream(buffer));
        List<string> filesToDelete = new();
        for (int i = 0; i < count; i++) filesToDelete.Add(memReader.ReadString());
        binaryReader.Close();
        memReader.Close();
        fileStreamTest.Close();
        foreach (string toDelete in filesToDelete)
        {
            string tpp = Path.GetFullPath(newPath + toDelete);
            if (!File.Exists(tpp))
            {
                Console.WriteLine("Error");
                return;
            }
        }

        DirectoryInfo directoryInfo = new(tempNewFilePath);
        FileInfo[] fileInfos = directoryInfo.GetFiles("*.*", SearchOption.AllDirectories);
        List<string> needToCreateFiles = new();
        foreach (FileInfo newFile in fileInfos)
        {
            string fullpaths = newFile.FullName;
            string relativityPath = fullpaths.Replace(tempNewFilePath, "");
            fullpaths = Path.GetFullPath(newPath + relativityPath);
            if (!File.Exists(fullpaths))
            {
                needToCreateFiles.Add(relativityPath);
            }
            else
            {
                Console.WriteLine("Error");
                return;
            }
        }

        Console.WriteLine("Updating.");
        foreach (string toDelete in filesToDelete)
        {
            string tpp = Path.GetFullPath(newPath + toDelete);
            if (File.Exists(tpp))
            {
                File.Delete(tpp);
            }
            else
            {
                Console.WriteLine("Error");
                return;
            }
        }

        foreach (string createFile in needToCreateFiles)
        {
            string source = Path.GetFullPath(tempNewFilePath + createFile);
            string destination = Path.GetFullPath(newPath + createFile);
            string? directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(directory))
            {
                Console.WriteLine("Error");
                return;
            }

            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.Copy(source, destination);
        }

        MyDelete(tempPath);
        Console.WriteLine("Update finished.");
    }
}