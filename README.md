# HEIC to Images

A small desktop app for converting HEIC/HEIF images to common image formats.

You can drag images into the app, choose the target format, review the output paths in a table, and convert everything in one batch.

## What It Uses

- .NET 8 target framework
- Avalonia UI for the desktop interface
- Avalonia DataGrid for the table
- CommunityToolkit.Mvvm for view models and commands
- Magick.NET for reading HEIC/HEIF files and writing JPG, PNG, WEBP, BMP, and TIFF

## Local Setup

```powershell
git clone <repo-url>
cd HeicToImages
dotnet restore HeicToImages.slnx
```

You need the .NET SDK installed. A newer SDK can build this project as long as it supports .NET 8 projects.

## Run Locally

```powershell
dotnet run --project src\HeicToImages.App
```

## Build

```powershell
dotnet build HeicToImages.slnx
```

## Basic Workflow

1. Drag HEIC files onto the drop area, or click it to select files.
2. New rows default to JPG.
3. Output paths default to the same folder and filename as the input, with the target extension.
4. Select rows and use **Apply Format** or **Choose Output Folder** to change them.
5. Click **Convert Selected** or **Convert All**.

## Project Layout

- `src/HeicToImages.App`: Avalonia desktop app and UI files.
- `src/HeicToImages.Application`: conversion logic, file rules, and view models.
