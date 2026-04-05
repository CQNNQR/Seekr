# Seekr - Disk Usage Analyzer

## Project Overview
Seekr is a native 64-bit Windows application for lightning-fast disk usage analysis with beautiful visualizations. Built with .NET 8 and WinForms for maximum performance and portability.

## Current Status: Phase 1 MVP Complete ✅

### Architecture
- **Framework**: .NET 8 (Long-Term Support)
- **GUI**: WinForms (native Windows performance)
- **Charting**: ScottPlot and LiveCharts2 for visualizations
- **Target**: Windows 64-bit, single portable executable

### Project Structure
```
Seekr/
├── Program.cs                 # Application entry point
├── Forms/
│   ├── MainForm.cs           # Main application window (complete)
│   └── AboutForm.cs          # About dialog (complete)
├── Services/
│   ├── DiskScanner.cs        # Core scanning engine (complete)
│   └── AnalysisService.cs    # Data analysis service (complete)
├── Models/
│   ├── FileSystemNode.cs     # File/folder data structure (complete)
│   └── ScanOptions.cs        # User preferences (complete)
└── Controls/
    └── TreemapControl.cs     # Treemap visualization (placeholder)
```

### Completed Features (Phase 1)
- ✅ Multithreaded directory scanning with cancellation
- ✅ Real-time progress reporting and status updates
- ✅ TreeView display with size-sorted results
- ✅ Graceful error handling for restricted folders
- ✅ Human-readable size formatting
- ✅ Responsive UI that doesn't lock up during scans
- ✅ Path selection with FolderBrowserDialog


### Development Guidelines
- Always maintain UI responsiveness with background threading
- Handle permission errors gracefully (many system folders are restricted)
- Use absolute paths consistently
- Implement proper resource disposal
- Test with various drive types and large directory structures
- Follow the established architecture patterns

### Key Classes
- `DiskScanner`: Core multithreaded scanning with progress reporting
- `FileSystemNode`: Hierarchical file system representation
- `AnalysisService`: Statistical analysis of scan results
- `MainForm`: Primary user interface with all controls

### Performance Considerations
- Parallel processing for file enumeration
- Cancellation token support for responsive cancellation
- Memory-efficient tree structure
- Progress reporting without UI blocking

### Build Commands
```bash
# Development build
dotnet build

# Release build
dotnet build -c Release

# Single-file portable executable
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true /p:EnableCompressionInSingleFile=true
```
