using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using HeicToImages.Application.Conversion;
using HeicToImages.Application.Files;
using HeicToImages.Application.Presentation;

namespace HeicToImages.App;

public sealed partial class MainWindow : Window
{
    private static readonly Geometry SystemThemeIcon = StreamGeometry.Parse(
        "M19.43 12.98c.04-.32.07-.65.07-.98s-.02-.66-.07-.98l2.11-1.65c.19-.15.24-.42.12-.64l-2-3.46c-.12-.22-.37-.31-.6-.22l-2.49 1a7.3 7.3 0 0 0-1.69-.98L14.5 2.42A.49.49 0 0 0 14 2h-4a.49.49 0 0 0-.5.42L9.12 5.07c-.6.24-1.17.56-1.69.98l-2.49-1c-.23-.08-.48 0-.6.22l-2 3.46c-.12.22-.07.49.12.64l2.11 1.65c-.04.32-.07.65-.07.98s.02.66.07.98l-2.11 1.65c-.19.15-.24.42-.12.64l2 3.46c.12.22.37.31.6.22l2.49-1c.52.4 1.08.73 1.69.98l.38 2.65c.04.24.25.42.5.42h4c.25 0 .46-.18.5-.42l.38-2.65c.6-.24 1.17-.56 1.69-.98l2.49 1c.23.08.48 0 .6-.22l2-3.46c.12-.22.07-.49-.12-.64l-2.11-1.65z M12 15.5A3.5 3.5 0 1 1 12 8a3.5 3.5 0 0 1 0 7.5z");

    private static readonly Geometry LightThemeIcon = StreamGeometry.Parse(
        "M6.76 4.84l-1.8-1.79-1.41 1.41 1.79 1.8 1.42-1.42z M1 13h3v-2H1v2z M11 1h2v3h-2V1z M20 11v2h3v-2h-3z M18.66 6.27l1.79-1.8-1.41-1.41-1.8 1.79 1.42 1.42z M17.24 19.16l1.8 1.79 1.41-1.41-1.79-1.8-1.42 1.42z M4.96 20.95l1.8-1.79-1.42-1.42-1.79 1.8 1.41 1.41z M11 20h2v3h-2v-3z M12 6a6 6 0 1 0 0 12 6 6 0 0 0 0-12z M12 16a4 4 0 1 1 0-8 4 4 0 0 1 0 8z");

    private static readonly Geometry DarkThemeIcon = StreamGeometry.Parse(
        "M12.74 2.03a9.5 9.5 0 1 0 9.23 9.23.75.75 0 0 0-1.18-.62 7 7 0 0 1-9.43-9.43.75.75 0 0 0-.62-1.18z M12 20a8 8 0 0 1-3.92-14.97A8.5 8.5 0 0 0 18.97 15.92 8 8 0 0 1 12 20z");

    private ThemeChoice _themeChoice = ThemeChoice.System;

    private static readonly FilePickerFileType HeicFileType = new("HEIC images")
    {
        Patterns = ["*.heic", "*.heif", "*.HEIC", "*.HEIF"],
    };

    public MainWindow()
    {
        InitializeComponent();
        LoadWindowIcon();
        ApplyThemeChoice();
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

    private void LoadWindowIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://HeicToImages.App/Assets/app-icon.ico"));
            Icon = new WindowIcon(stream);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            // The icon is cosmetic; startup should still succeed if the resource cannot load.
        }
    }

    private void ThemeToggleButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _themeChoice = _themeChoice switch
        {
            ThemeChoice.System => ThemeChoice.Light,
            ThemeChoice.Light => ThemeChoice.Dark,
            _ => ThemeChoice.System,
        };

        ApplyThemeChoice();
        e.Handled = true;
    }

    private void ApplyThemeChoice()
    {
        if (Avalonia.Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = _themeChoice switch
        {
            ThemeChoice.Light => ThemeVariant.Light,
            ThemeChoice.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        if (this.FindControl<Button>("ThemeToggleButton") is { } button)
        {
            button.Content = new PathIcon
            {
                Data = GetThemeIcon(_themeChoice),
                Width = 18,
                Height = 18,
            };
            ToolTip.SetTip(button, $"Click to change theme to {GetThemeLabel(GetNextThemeChoice(_themeChoice))}");
        }
    }

    private static Geometry GetThemeIcon(ThemeChoice choice) =>
        choice switch
        {
            ThemeChoice.Light => LightThemeIcon,
            ThemeChoice.Dark => DarkThemeIcon,
            _ => SystemThemeIcon,
        };

    private static ThemeChoice GetNextThemeChoice(ThemeChoice choice) =>
        choice switch
        {
            ThemeChoice.System => ThemeChoice.Light,
            ThemeChoice.Light => ThemeChoice.Dark,
            _ => ThemeChoice.System,
        };

    private static string GetThemeLabel(ThemeChoice choice) =>
        choice switch
        {
            ThemeChoice.Light => "Light",
            ThemeChoice.Dark => "Dark",
            _ => "System",
        };

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
            FileTypeFilter = [HeicFileType],
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
            Patterns = [$"*.{format}"],
        };

        var options = new FilePickerSaveOptions
        {
            Title = "Select output image",
            SuggestedFileName = GetSuggestedOutputFileName(file),
            DefaultExtension = format,
            FileTypeChoices = [outputType],
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
            FileTypeFilter = [HeicFileType],
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

    private enum ThemeChoice
    {
        System,
        Light,
        Dark,
    }
}
