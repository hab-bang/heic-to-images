namespace HeicToImages.Application.Conversion;

public interface IImageConversionService
{
    Task<ImageConversionSummary> ConvertAsync(
        ImageConversionRequest request,
        CancellationToken cancellationToken);
}
