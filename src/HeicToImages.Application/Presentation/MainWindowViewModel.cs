using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeicToImages.Application.Conversion;
using HeicToImages.Application.Files;

namespace HeicToImages.Application.Presentation;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IImageConversionService _conversionService;
    private readonly IOutputPathPolicy _outputPathPolicy;
    private readonly StringComparer _pathComparer;
    private readonly List<QueuedImageFileViewModel> _selectedFiles = new();
    private CancellationTokenSource? _conversionCancellation;
    private bool _isSynchronizingSelection;

    [ObservableProperty]
    private string selectedTargetFormat = ImageFormatCatalog.Jpg;

    [ObservableProperty]
    private bool isConverting;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    private string progressText = "Idle";

    [ObservableProperty]
    private bool isProgressVisible;

    public MainWindowViewModel(
        IImageConversionService conversionService,
        IOutputPathPolicy outputPathPolicy)
    {
        _conversionService = conversionService;
        _outputPathPolicy = outputPathPolicy;
        _pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        Files.CollectionChanged += FilesChanged;
    }

    public ObservableCollection<QueuedImageFileViewModel> Files { get; } = new();

    public IReadOnlyList<string> TargetFormats => ImageFormatCatalog.DisplayFormats;

    public bool HasFiles => Files.Count > 0;

    public string FileCountText => $"{Files.Count} image{Plural(Files.Count)}";

    public int SelectedFileCount => _selectedFiles.Count;

    private bool CanConvertAllFiles() => !IsConverting && Files.Any(file => file.CanConvert);

    private bool CanConvertSelectedFiles() => !IsConverting && _selectedFiles.Any(file => file.CanConvert);

    private bool CanCancelConversion() => IsConverting;

    private bool CanClearFiles() => !IsConverting && Files.Count > 0;

    private bool CanDeleteSelectedFiles() => !IsConverting && _selectedFiles.Count > 0;

    private bool CanApplyFormat() => !IsConverting && Files.Count > 0;

    public void AddFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (IsConverting)
        {
            StatusMessage = "Cannot add files while conversion is running.";
            return;
        }

        int added = 0;
        int ignored = 0;

        foreach (string path in ExpandCandidatePaths(paths))
        {
            if (TryAddFile(path))
            {
                added++;
            }
            else
            {
                ignored++;
            }
        }

        StatusMessage = (added, ignored) switch
        {
            (0, 0) => "Ready",
            (> 0, 0) => $"Added {added} HEIC image{Plural(added)}.",
            (0, > 0) => $"Ignored {ignored} file{Plural(ignored)}.",
            _ => $"Added {added} HEIC image{Plural(added)}; ignored {ignored} file{Plural(ignored)}.",
        };

        RefreshCommandState();
    }

    public void SetSelectedFiles(IEnumerable<QueuedImageFileViewModel> selectedFiles)
    {
        ArgumentNullException.ThrowIfNull(selectedFiles);

        var selectedSet = selectedFiles
            .Where(file => Files.Contains(file))
            .ToHashSet();

        _isSynchronizingSelection = true;
        try
        {
            foreach (var file in Files)
            {
                file.IsSelected = selectedSet.Contains(file);
            }
        }
        finally
        {
            _isSynchronizingSelection = false;
        }

        RefreshSelectedFiles();
    }

    private void RefreshSelectedFiles()
    {
        _selectedFiles.Clear();
        _selectedFiles.AddRange(Files.Where(file => file.IsSelected));

        OnPropertyChanged(nameof(SelectedFileCount));
        RefreshCommandState();
    }

    public bool TryChangeSourceFile(
        QueuedImageFileViewModel file,
        string newSourcePath,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(file);
        message = string.Empty;

        if (IsConverting)
        {
            message = "Cannot change files while conversion is running.";
            return false;
        }

        if (!Files.Contains(file))
        {
            message = "Selected row is no longer queued.";
            return false;
        }

        if (!ImageFileFilter.IsHeicFile(newSourcePath))
        {
            message = "Select a HEIC or HEIF file.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(newSourcePath);
        }
        catch (Exception ex) when (IsPathException(ex))
        {
            message = ex.Message;
            return false;
        }

        string previousDefaultOutputPath = _outputPathPolicy.CreateOutputPath(file.SourcePath, file.TargetFormat);
        bool shouldRefreshOutputPath =
            string.IsNullOrWhiteSpace(file.OutputPath) ||
            AreEquivalentPaths(file.OutputPath, previousDefaultOutputPath);

        file.SourcePath = fullPath;
        if (shouldRefreshOutputPath)
        {
            file.OutputPath = _outputPathPolicy.CreateOutputPath(fullPath, file.TargetFormat);
        }

        file.MarkPending("Input changed");
        StatusMessage = $"Selected {file.FileName}.";
        RefreshCommandState();
        return true;
    }

    public void SetOutputFolderForSelectionOrAll(string outputFolder)
    {
        if (IsConverting)
        {
            StatusMessage = "Cannot change output folders while conversion is running.";
            return;
        }

        IReadOnlyList<QueuedImageFileViewModel> targets = SelectionOrAll();
        if (targets.Count == 0)
        {
            StatusMessage = "No images queued.";
            return;
        }

        string folder = Path.GetFullPath(outputFolder);
        foreach (var file in targets)
        {
            string outputName = string.IsNullOrWhiteSpace(file.OutputPath)
                ? $"{Path.GetFileNameWithoutExtension(file.SourcePath)}.{file.TargetFormat}"
                : Path.GetFileName(file.OutputPath);
            file.OutputPath = Path.Combine(folder, outputName);
            file.MarkPending("Output folder changed");
        }

        StatusMessage = $"Updated output folder for {targets.Count} image{Plural(targets.Count)}.";
        RefreshCommandState();
    }

    public void SetOutputPath(QueuedImageFileViewModel file, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (IsConverting || !Files.Contains(file))
        {
            return;
        }

        string fullPath = Path.GetFullPath(outputPath);
        file.OutputPath = EnsureExtension(fullPath, file.TargetFormat);
        file.MarkPending("Output changed");
        RefreshCommandState();
    }

    [RelayCommand(CanExecute = nameof(CanApplyFormat))]
    private void ApplyFormat()
    {
        string targetFormat = ImageFormatCatalog.Normalize(SelectedTargetFormat);
        IReadOnlyList<QueuedImageFileViewModel> targets = SelectionOrAll();

        foreach (var file in targets)
        {
            file.TargetFormat = targetFormat;
        }

        StatusMessage = $"Applied {targetFormat.ToUpperInvariant()} to {targets.Count} image{Plural(targets.Count)}.";
        RefreshCommandState();
    }

    [RelayCommand(CanExecute = nameof(CanConvertAllFiles))]
    private Task ConvertAllAsync() =>
        ConvertFilesAsync(Files.Where(file => file.CanConvert).ToArray());

    [RelayCommand(CanExecute = nameof(CanConvertSelectedFiles))]
    private Task ConvertSelectedAsync()
    {
        var selectedSet = _selectedFiles.ToHashSet();
        var candidates = Files
            .Where(file => selectedSet.Contains(file) && file.CanConvert)
            .ToArray();

        return ConvertFilesAsync(candidates);
    }

    [RelayCommand(CanExecute = nameof(CanCancelConversion))]
    private void Cancel()
    {
        _conversionCancellation?.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanClearFiles))]
    private void ClearFiles()
    {
        foreach (var file in Files)
        {
            file.PropertyChanged -= QueuedFileChanged;
        }

        _selectedFiles.Clear();
        Files.Clear();
        StatusMessage = "Ready";
        ResetProgress();
        OnPropertyChanged(nameof(SelectedFileCount));
        RefreshCommandState();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedFiles))]
    private void DeleteSelected()
    {
        if (_selectedFiles.Count == 0)
        {
            return;
        }

        foreach (var file in _selectedFiles.ToArray())
        {
            file.PropertyChanged -= QueuedFileChanged;
            Files.Remove(file);
        }

        _selectedFiles.Clear();
        StatusMessage = $"{Files.Count} image{Plural(Files.Count)} queued.";
        OnPropertyChanged(nameof(SelectedFileCount));
        RefreshCommandState();
    }

    private async Task ConvertFilesAsync(IReadOnlyList<QueuedImageFileViewModel> candidates)
    {
        if (candidates.Count == 0)
        {
            StatusMessage = "No pending HEIC images.";
            return;
        }

        _conversionCancellation = new CancellationTokenSource();
        IsConverting = true;
        BeginProgress(candidates.Count);
        int converted = 0;
        int failed = 0;
        int completed = 0;

        try
        {
            foreach (var file in candidates)
            {
                if (_conversionCancellation.IsCancellationRequested)
                {
                    break;
                }

                file.MarkConverting();
                StatusMessage = $"Converting {file.FileName}...";

                try
                {
                    if (!TryCreateConversionRequest(file, out var request, out string errorMessage))
                    {
                        file.MarkFailed(errorMessage);
                        failed++;
                        completed++;
                        UpdateProgress(completed, candidates.Count);
                        continue;
                    }

                    var summary = await _conversionService.ConvertAsync(
                        request,
                        _conversionCancellation.Token);

                    file.MarkConverted(summary);
                    converted++;
                    completed++;
                    UpdateProgress(completed, candidates.Count);
                }
                catch (OperationCanceledException)
                {
                    file.MarkPending("Cancelled");
                    break;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not AccessViolationException)
                {
                    file.MarkFailed(ex.Message);
                    failed++;
                    completed++;
                    UpdateProgress(completed, candidates.Count);
                }
            }
        }
        finally
        {
            bool cancelled = _conversionCancellation.IsCancellationRequested;
            _conversionCancellation.Dispose();
            _conversionCancellation = null;
            IsConverting = false;

            StatusMessage = cancelled
                ? $"Conversion cancelled. Completed {converted}; failed {failed}."
                : $"Conversion finished. Completed {converted}; failed {failed}.";
            FinishProgress(cancelled, completed, candidates.Count);
        }
    }

    private bool TryAddFile(string path)
    {
        if (!ImageFileFilter.IsHeicFile(path) || !File.Exists(path))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (IsPathException(ex))
        {
            return false;
        }

        if (Files.Any(file => AreEquivalentPaths(file.SourcePath, fullPath)))
        {
            return false;
        }

        string targetFormat = ImageFormatCatalog.Normalize(SelectedTargetFormat);
        string outputPath = _outputPathPolicy.CreateOutputPath(fullPath, targetFormat);
        var item = new QueuedImageFileViewModel(fullPath, outputPath, targetFormat);
        item.PropertyChanged += QueuedFileChanged;
        Files.Add(item);
        return true;
    }

    private static IEnumerable<string> ExpandCandidatePaths(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(path);
                }
                catch (Exception ex) when (IsPathException(ex))
                {
                    continue;
                }

                foreach (string file in files)
                {
                    yield return file;
                }

                continue;
            }

            yield return path;
        }
    }

    private IReadOnlyList<QueuedImageFileViewModel> SelectionOrAll() =>
        _selectedFiles.Count > 0 ? _selectedFiles.ToArray() : Files.ToArray();

    private bool TryCreateConversionRequest(
        QueuedImageFileViewModel file,
        out ImageConversionRequest request,
        out string errorMessage)
    {
        request = null!;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(file.OutputPath))
        {
            errorMessage = "Output path is required.";
            return false;
        }

        try
        {
            string outputPath = EnsureExtension(Path.GetFullPath(file.OutputPath), file.TargetFormat);
            file.OutputPath = outputPath;
            request = new ImageConversionRequest(
                Path.GetFullPath(file.SourcePath),
                outputPath,
                file.TargetFormat,
                Overwrite: true);
            return true;
        }
        catch (Exception ex) when (IsPathException(ex))
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private void FilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(FileCountText));
        RefreshCommandState();
    }

    private void QueuedFileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueuedImageFileViewModel.IsSelected) &&
            !_isSynchronizingSelection)
        {
            RefreshSelectedFiles();
            return;
        }

        if (e.PropertyName is nameof(QueuedImageFileViewModel.CanConvert)
            or nameof(QueuedImageFileViewModel.Status)
            or nameof(QueuedImageFileViewModel.OutputPath)
            or nameof(QueuedImageFileViewModel.TargetFormat))
        {
            RefreshCommandState();
        }
    }

    partial void OnIsConvertingChanged(bool value)
    {
        RefreshCommandState();
    }

    partial void OnSelectedTargetFormatChanged(string value)
    {
        ApplyFormatCommand.NotifyCanExecuteChanged();
    }

    private void RefreshCommandState()
    {
        ConvertAllCommand.NotifyCanExecuteChanged();
        ConvertSelectedCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ClearFilesCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        ApplyFormatCommand.NotifyCanExecuteChanged();
    }

    private void BeginProgress(int total)
    {
        IsProgressVisible = total > 0;
        UpdateProgress(completed: 0, total);
    }

    private void UpdateProgress(int completed, int total)
    {
        ProgressPercent = total <= 0
            ? 0
            : Math.Round(completed * 100d / total, MidpointRounding.AwayFromZero);
        ProgressText = $"{ProgressPercent:0}% - {completed} / {total}";
    }

    private void FinishProgress(bool cancelled, int completed, int total)
    {
        if (total <= 0)
        {
            ResetProgress();
            return;
        }

        UpdateProgress(completed, total);
        if (cancelled)
        {
            ProgressText = $"Cancelled - {ProgressPercent:0}% - {completed} / {total}";
        }
    }

    private void ResetProgress()
    {
        ProgressPercent = 0;
        ProgressText = "Idle";
        IsProgressVisible = false;
    }

    private bool AreEquivalentPaths(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return _pathComparer.Equals(Path.GetFullPath(left), Path.GetFullPath(right));
        }
        catch (Exception ex) when (IsPathException(ex))
        {
            return _pathComparer.Equals(left.Trim(), right.Trim());
        }
    }

    private static string EnsureExtension(string path, string targetFormat)
    {
        string format = ImageFormatCatalog.Normalize(targetFormat);
        return Path.ChangeExtension(path, $".{format}");
    }

    private static bool IsPathException(Exception ex) =>
        ex is ArgumentException
            or IOException
            or NotSupportedException
            or PathTooLongException
            or UnauthorizedAccessException;

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
