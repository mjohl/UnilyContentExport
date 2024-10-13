public static class NewsItemHelpers
{
    public static string SanitizeFileName(string fileName)
    {
        char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
        foreach (char invalidChar in invalidChars)
        {
            fileName = fileName.Replace(invalidChar, '_');
        }
        return fileName;
    }
}