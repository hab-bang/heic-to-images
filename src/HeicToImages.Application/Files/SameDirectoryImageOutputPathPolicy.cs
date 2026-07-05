namespace HeicToImages.Application.Files;

public sealed class SameDirectoryImageOutputPathPolicy : IOutputPathPolicy
{
    public string CreateOutputPath(string sourcePath, string targetFormat)
    {
        string fullSourcePath = Path.GetFullPath(sourcePath);
        string format = ImageFormatCatalog.Normalize(targetFormat);
        string? directory = Path.GetDirectoryName(fullSourcePath);
        string fileName = $"{Path.GetFileNameWithoutExtension(fullSourcePath)}.{format}";
        return string.IsNullOrWhiteSpace(directory)
            ? fileName
            : Path.Combine(directory, fileName);
    }
}
