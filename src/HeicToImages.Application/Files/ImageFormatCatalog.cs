namespace HeicToImages.Application.Files;

public static class ImageFormatCatalog
{
    public const string Jpg = "jpg";
    public const string Png = "png";
    public const string Webp = "webp";
    public const string Bmp = "bmp";
    public const string Tiff = "tiff";

    public static IReadOnlyList<string> DisplayFormats { get; } =
        new[] { Jpg, Png, Webp, Bmp, Tiff };

    public static string Normalize(string format)
    {
        string normalized = format.Trim().TrimStart('.').ToLowerInvariant();
        if (normalized == "jpeg")
        {
            normalized = Jpg;
        }

        if (!DisplayFormats.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            string supported = string.Join(", ", DisplayFormats);
            throw new ArgumentOutOfRangeException(nameof(format), format, $"Supported formats: {supported}");
        }

        return normalized;
    }
}
