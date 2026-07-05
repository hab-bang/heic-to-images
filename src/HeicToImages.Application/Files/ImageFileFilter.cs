namespace HeicToImages.Application.Files;

public static class ImageFileFilter
{
    private static readonly HashSet<string> HeicExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".heic",
        ".heif",
    };

    public static bool IsHeicFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return HeicExtensions.Contains(Path.GetExtension(path));
        }
        catch (Exception ex) when (IsPathException(ex))
        {
            return false;
        }
    }

    private static bool IsPathException(Exception ex) =>
        ex is ArgumentException
            or IOException
            or NotSupportedException
            or PathTooLongException
            or UnauthorizedAccessException;
}
