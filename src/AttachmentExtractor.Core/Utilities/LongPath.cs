namespace AttachmentExtractor.Core.Utilities;

public static class LongPath
{
    private const int ExtendedPrefixThreshold = 240;

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var fullPath = path.StartsWith(@"\\?\", StringComparison.Ordinal)
            ? path
            : Path.GetFullPath(path);

        return fullPath.Length >= ExtendedPrefixThreshold || fullPath.StartsWith(@"\\", StringComparison.Ordinal)
            ? ToExtended(fullPath)
            : fullPath;
    }

    public static string ToExtended(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return path;
        }

        return path.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + path[2..]
            : @"\\?\" + path;
    }

    public static bool FileExists(string path)
    {
        try
        {
            return File.Exists(Normalize(path));
        }
        catch
        {
            return false;
        }
    }

    public static bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(Normalize(path));
        }
        catch
        {
            return false;
        }
    }

    public static void CreateDirectory(string path)
    {
        Directory.CreateDirectory(Normalize(path));
    }

    public static void Move(string sourcePath, string destinationPath, bool overwrite)
    {
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);

        try
        {
            File.Move(source, destination, overwrite);
        }
        catch (IOException)
        {
            File.Copy(source, destination, overwrite);
            File.Delete(source);
        }
    }

    public static void SetLastWriteTime(string path, DateTime timestamp)
    {
        var normalized = Normalize(path);
        File.SetLastWriteTime(normalized, timestamp);
        File.SetCreationTime(normalized, timestamp);
    }
}
