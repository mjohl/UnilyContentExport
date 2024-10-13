using Microsoft.Extensions.Configuration;
using Serilog;

public static class IOHelper
{
    public static ILogger Logger { get; private set; } = null!;
    private static char[] invalidPathChars = Path.GetInvalidPathChars().Concat(new[] { '?' }).ToArray();
    public static void InitIOHelper(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
    }

    public static string SanitizeFileName(string nodeName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (var invalidChar in invalidChars)
        {
            nodeName = nodeName.Replace(invalidChar, '_');
        }
        return nodeName;
    }

    public static string SanitizePathName(string path)
    {
        char[] invalidPathChars = Path.GetInvalidPathChars();
        foreach (var invalidChar in invalidPathChars)
        {
            path = path.Replace(invalidChar, '_');
        }

        // Ensures the drive letter format is retained (e.g., D:\)
        if (path.Length > 2 && path[1] == ':')
        {
            return path.Substring(0, 3) + path.Substring(3).Replace(':', '_');
        }

        return path.Replace(':', '_'); // Handle colon replacements in paths
    }

    public static string TruncateFileName(string name, int maxLength)
    {
        return name.Length > maxLength ? name.Substring(0, maxLength) : name;
    }

    public static void CreateDirectoryIfNotExists(string folderPath)
    {
        // Sanitize the folder path before creating it
        folderPath = SanitizePathName(folderPath);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
    }
}
