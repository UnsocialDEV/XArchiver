namespace XArchiver.Core.Utilities;

public static class FileNameSanitizer
{
    public static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "item";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        return sanitized.Trim().Trim('.').Replace(' ', '_');
    }
}
