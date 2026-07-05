using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using HeicToImages.Application.Conversion;
using HeicToImages.Application.Files;
using HeicToImages.Application.Presentation;

namespace HeicToImages.App;

public sealed partial class MainWindow : Window
{
    private static readonly FilePickerFileType HeicFileType = new("HEIC images")
    {
        Patterns = new[] { "*.heic", "*.heif", "*.HEIC", "*.HEIF" },
    };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(
            new MagickImageConversionService(),
            new SameDirectoryImageOutputPathPolicy());
        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DropEvent, Drop);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ItemsGrid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetSelectedFiles(grid.SelectedItems.OfType<QueuedImageFileViewModel>());
        }
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        var paths = e.DataTransfer.TryGetFiles()?
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();

        if (paths is { Length: > 0 } && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.AddFiles(paths);
        }

        e.Handled = true;
    }

    private async void AddFilesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await PickInputFilesAsync();
        e.Handled = true;
    }

    private async void DropZone_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        await PickInputFilesAsync();
        e.Handled = true;
    }

    private async void ChooseOutputFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var pickedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose output folder",
            AllowMultiple = false,
        });

        string? selectedPath = pickedFolders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            viewModel.SetOutputFolderForSelectionOrAll(selectedPath);
        }

        e.Handled = true;
    }

    private async void InputPathCellButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: QueuedImageFileViewModel file } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var options = new FilePickerOpenOptions
        {
            Title = "Select HEIC image",
            AllowMultiple = false,
            FileTypeFilter = new[] { HeicFileType },
        };

        string? directory = TryGetExistingDirectory(file.SourcePath);
        if (directory is not null)
        {
            options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(directory);
        }

        var pickedFiles = await StorageProvider.OpenFilePickerAsync(options);
        string? selectedPath = pickedFiles.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        if (!viewModel.TryChangeSourceFile(file, selectedPath, out string message))
        {
            viewModel.StatusMessage = message;
        }

        e.Handled = true;
    }

    private async void OutputPathCellButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: QueuedImageFileViewModel file } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string format = ImageFormatCatalog.Normalize(file.TargetFormat);
        var outputType = new FilePickerFileType($"{format.ToUpperInvariant()} images")
        {
            Patterns = new[] { $"*.{format}" },
        };

        var options = new FilePickerSaveOptions
        {
            Title = "Select output image",
            SuggestedFileName = GetSuggestedOutputFileName(file),
            DefaultExtension = format,
            FileTypeChoices = new[] { outputType },
            SuggestedFileType = outputType,
            ShowOverwritePrompt = false,
        };

        string seedPath = string.IsNullOrWhiteSpace(file.OutputPath)
            ? file.SourcePath
            : file.OutputPath;
        string? directory = TryGetExistingDirectory(seedPath);
        if (directory is not null)
        {
            options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(directory);
        }

        var pickedFile = await StorageProvider.SaveFilePickerAsync(options);
        string? selectedPath = pickedFile?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            viewModel.SetOutputPath(file, selectedPath);
        }

        e.Handled = true;
    }

    private async Task PickInputFilesAsync()
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.IsConverting)
        {
            return;
        }

        var pickedFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose HEIC images",
            AllowMultiple = true,
            FileTypeFilter = new[] { HeicFileType },
        });

        var paths = pickedFiles
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();

        if (paths.Length > 0)
        {
            viewModel.AddFiles(paths);
        }
    }

    private static string GetSuggestedOutputFileName(QueuedImageFileViewModel file)
    {
        string seedPath = string.IsNullOrWhiteSpace(file.OutputPath)
            ? file.SourcePath
            : file.OutputPath;

        try
        {
            string fileName = Path.GetFileName(seedPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return $"output.{file.TargetFormat}";
            }

            return Path.ChangeExtension(fileName, $".{file.TargetFormat}");
        }
        catch (Exception ex) when (IsPathException(ex))
        {
            return $"output.{file.TargetFormat}";
        }
    }

    private static string? TryGetExistingDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                ? directory
                : null;
        }
        catch (Exception ex) when (IsPathException(ex))
        {
            return null;
        }
    }

    private static bool IsPathException(Exception ex) =>
        ex is ArgumentException
            or IOException
            or NotSupportedException
            or PathTooLongException
            or UnauthorizedAccessException;
}
