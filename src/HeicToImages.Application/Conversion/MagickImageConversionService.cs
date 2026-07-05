using HeicToImages.Application.Files;
using ImageMagick;

namespace HeicToImages.Application.Conversion;

public sealed class MagickImageConversionService : IImageConversionService
{
    public Task<ImageConversionSummary> ConvertAsync(
        ImageConversionRequest request,
        CancellationToken cancellationToken) =>
        Task.Run(() => Convert(request, cancellationToken), cancellationToken);

    private static ImageConversionSummary Convert(
        ImageConversionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(request.SourcePath))
        {
            throw new FileNotFoundException("Input image does not exist.", request.SourcePath);
        }

        if (!ImageFileFilter.IsHeicFile(request.SourcePath))
        {
            throw new InvalidOperationException("Input image must be a HEIC or HEIF file.");
        }

        string targetFormat = ImageFormatCatalog.Normalize(request.TargetFormat);
        string outputPath = Path.GetFullPath(request.OutputPath);
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (File.Exists(outputPath) && !request.Overwrite)
        {
            throw new IOException($"Output file already exists: {outputPath}");
        }

        using var image = new MagickImage(request.SourcePath);
        image.AutoOrient();
        image.Strip();
        image.Format = ToMagickFormat(targetFormat);

        if (targetFormat == ImageFormatCatalog.Jpg)
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
            image.Quality = 95;
        }

        cancellationToken.ThrowIfCancellationRequested();
        image.Write(outputPath);

        long bytes = new FileInfo(outputPath).Length;
        return new ImageConversionSummary(outputPath, bytes);
    }

    private static MagickFormat ToMagickFormat(string targetFormat) =>
        ImageFormatCatalog.Normalize(targetFormat) switch
        {
            ImageFormatCatalog.Jpg => MagickFormat.Jpeg,
            ImageFormatCatalog.Png => MagickFormat.Png,
            ImageFormatCatalog.Webp => MagickFormat.WebP,
            ImageFormatCatalog.Bmp => MagickFormat.Bmp,
            ImageFormatCatalog.Tiff => MagickFormat.Tiff,
            _ => throw new ArgumentOutOfRangeException(nameof(targetFormat), targetFormat, null),
        };
}
