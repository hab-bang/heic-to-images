using CommunityToolkit.Mvvm.ComponentModel;
using HeicToImages.Application.Conversion;
using HeicToImages.Application.Files;

namespace HeicToImages.Application.Presentation;

public sealed class QueuedImageFileViewModel : ObservableObject
{
    private string _sourcePath;
    private string _outputPath;
    private string _targetFormat;
    private QueuedImageStatus _status;
    private string _message = "Ready";

    public QueuedImageFileViewModel(string sourcePath, string outputPath, string targetFormat)
    {
        _sourcePath = sourcePath;
        _outputPath = outputPath;
        _targetFormat = ImageFormatCatalog.Normalize(targetFormat);
        _status = QueuedImageStatus.Pending;
    }

    public string SourcePath
    {
        get => _sourcePath;
        set
        {
            if (SetProperty(ref _sourcePath, value))
            {
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(DirectoryPath));
                OnPropertyChanged(nameof(CanConvert));
            }
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set => SetProperty(ref _outputPath, value);
    }

    public string TargetFormat
    {
        get => _targetFormat;
        set
        {
            string normalized = ImageFormatCatalog.Normalize(value);
            if (!SetProperty(ref _targetFormat, normalized))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(OutputPath))
            {
                OutputPath = Path.ChangeExtension(OutputPath, $".{normalized}");
            }

            MarkPending("Format changed");
        }
    }

    public QueuedImageStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(CanConvert));
                OnPropertyChanged(nameof(CanEdit));
            }
        }
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string FileName => Path.GetFileName(SourcePath);

    public string DirectoryPath => Path.GetDirectoryName(SourcePath) ?? string.Empty;

    public string StatusText => Status switch
    {
        QueuedImageStatus.Pending => "Pending",
        QueuedImageStatus.Converting => "Converting",
        QueuedImageStatus.Converted => "Done",
        QueuedImageStatus.Failed => "Failed",
        _ => Status.ToString(),
    };

    public bool CanConvert =>
        Status != QueuedImageStatus.Converting &&
        ImageFileFilter.IsHeicFile(SourcePath) &&
        !string.IsNullOrWhiteSpace(OutputPath);

    public bool CanEdit => Status != QueuedImageStatus.Converting;

    public void MarkPending(string message)
    {
        Status = QueuedImageStatus.Pending;
        Message = message;
    }

    public void MarkConverting()
    {
        Status = QueuedImageStatus.Converting;
        Message = "Converting...";
    }

    public void MarkConverted(ImageConversionSummary summary)
    {
        OutputPath = summary.OutputPath;
        Status = QueuedImageStatus.Converted;
        Message = $"{FormatBytes(summary.OutputBytes)} written";
    }

    public void MarkFailed(string message)
    {
        Status = QueuedImageStatus.Failed;
        Message = string.IsNullOrWhiteSpace(message) ? "Conversion failed." : message;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        double kib = bytes / 1024d;
        if (kib < 1024)
        {
            return $"{kib:0.0} KB";
        }

        return $"{kib / 1024d:0.0} MB";
    }
}
