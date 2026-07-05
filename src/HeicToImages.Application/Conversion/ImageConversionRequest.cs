namespace HeicToImages.Application.Conversion;

public sealed record ImageConversionRequest(
    string SourcePath,
    string OutputPath,
    string TargetFormat,
    bool Overwrite);
