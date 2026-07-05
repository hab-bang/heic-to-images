namespace HeicToImages.Application.Files;

public interface IOutputPathPolicy
{
    string CreateOutputPath(string sourcePath, string targetFormat);
}
